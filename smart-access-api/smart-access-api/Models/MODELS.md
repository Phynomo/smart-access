# Modelos de datos — ResidentPass

Este documento explica **por qué existe cada modelo** y cómo se mapea a los requerimientos
del proyecto (PDF `Proyecto_ResidentPass.pdf`). El backend usa Firestore, así que cada
modelo corresponde a una colección (excepto `AccessStatistics`, que es un agregado en memoria).

> **Estilo de implementación.** Los modelos están anotados con `[FirestoreData]` /
> `[FirestoreProperty("nombreCamelCase")]` para que el SDK los serialice automáticamente
> con `doc.ConvertTo<T>()` y `collection.Document(id).SetAsync(obj)`. La capa de
> persistencia (`FirestoreContext` + configuraciones por entidad) está documentada al
> final de este archivo, en la sección **"Capa de Persistencia"**.

---

## 1. `User`

**Colección Firestore:** `users`

Representa la cuenta de cualquier persona que inicia sesión en la plataforma:
administrador, personal de seguridad o residente.

| Campo | Por qué |
|---|---|
| `Id`, `Name`, `Email`, `PasswordHash`, `CreatedAt` | Base de toda cuenta autenticada. |
| `HouseNumber` | El PDF pide **login flexible: correo o número de casa**, así que la casa debe vivir en `User` para poder buscar por ella en `AuthService.Login`. |
| `Role` (`admin` / `security` / `resident`) | El PDF define **tres tipos de usuarios** con paneles distintos; el rol gobierna la autorización en los endpoints y la navegación en Angular. |
| `QrPermanentId` | El PDF dice literalmente *"el campo `qrPermanentId` referencia el QR permanente asignado al residente"*. Permite resolver "¿cuál es mi QR?" en una sola lectura, sin recorrer la colección `QRCodes`. Es `null` para admin y seguridad. |
| `IsActive` | El PDF pide **desactivar sin borrar historial**: con este flag, los eventos pasados siguen apuntando a un `User` válido aunque la cuenta esté deshabilitada. |

---

## 2. `Resident`

**Colección Firestore:** `residents`

Contiene la **información de la persona y su vivienda**. Está separada de `User` porque
no toda cuenta es residente (admin y seguridad existen como `User` pero no como
`Resident`), y porque el residente tiene datos que no pertenecen a la autenticación
(vehículo, foto, contador de QR).

| Campo | Por qué |
|---|---|
| `UserId` | Une el residente con su cuenta de login (relación 1‑a‑1). Permite navegar de evento → residente → usuario. |
| `Name`, `HouseNumber`, `Email` | Pedidos explícitamente en *Gestión de Residentes* del panel de administrador. |
| *(vehículos)* | **Ya no van en `Resident`.** Viven en la colección `vehicles` con `ResidentId` como FK porque un residente puede tener varios vehículos. Ver §3. |
| `PhotoUrl` | El PDF define la carpeta `/residents/photos/` en Firebase Storage. Se guarda la URL pública, no el binario. |
| `ActivePermanentQrCount` | El PDF pide un **límite configurable de QR permanentes activos por residencia**. Cachear el contador aquí evita un `count(*)` sobre `QRCodes` en cada generación. Se actualiza dentro de la transacción que crea o revoca un QR permanente. |
| `IsActive` | Permite *"desactivar residentes sin eliminar su historial"* (requerimiento literal del PDF). |
| `CreatedAt`, `CreatedBy` | El PDF pide rastrear **quién y cuándo** registró al residente — auditoría del panel de admin. |

---

## 3. `Vehicle`

**Colección Firestore:** `vehicles`

Cada vehículo registrado a nombre de un residente. Se separa de `Resident` porque **un
residente puede tener N vehículos** (típicamente 1‑3, pero el sistema no debe limitarlos
artificialmente). Mantenerlo como colección top-level — en lugar de array embebido o
sub-colección — permite:

- Buscar por placa en O(1) con `WhereEqualTo("Plate", ...)` desde el panel de seguridad.
- Hacer CRUD individual sin reescribir el documento del residente entero.
- Mantener consistencia con el patrón actual de `FirebaseService.GetCollection(...)`.

| Campo | Por qué |
|---|---|
| `ResidentId` | FK al dueño. Permite listar "los vehículos de este residente" y validar permisos. |
| `Plate` | Llave de búsqueda principal del guardia. Debe ser única en el sistema (validar en `VehicleService.Create`). |
| `Brand`, `Model`, `Color`, `Year` | Datos descriptivos para identificación visual en la garita y en reportes. |
| `IsActive` | Permite "retirar" un vehículo sin borrar el `AccessEvent` histórico que lo referencia (mismo criterio que `Resident.IsActive`). |
| `CreatedAt` | Auditoría básica. |

---

## 4. `QRCode`

**Colección Firestore:** `qrcodes`

Cada QR emitido por el sistema, sea el permanente del residente o uno de visita.

| Campo | Por qué |
|---|---|
| `ResidentId` | Todo QR pertenece a un residente — soporta la notificación en tiempo real ("avisar al residente cuando su visitante entra") y el filtro de "mis QR" en el panel del residente. |
| `VisitorName` | Solo aplica a QR de visita; `null` para QR permanente del propio residente. |
| `QrType` (`permanent` / `date` / `long_term`) | El PDF distingue tres tipos: permanente del residente, de visita por fecha, y de larga duración. Cada uno tiene reglas distintas de expiración y de límite. |
| `ValidDate` | Solo para `date`: el QR **vence al finalizar el día indicado**. |
| `ExpiresAt` | Vencimiento absoluto (timestamp). Para `long_term` define la ventana; para `permanent` puede ir `null`. Tenerlo separado de `ValidDate` permite una validación uniforme en backend (`now > ExpiresAt → rechazar`). |
| `IsUsed` | El PDF pide **marcado atómico de QR como utilizado** dentro de una transacción de Firestore para prevenir reutilización (Escenario 2). |
| `IsRevoked` | El residente puede **revocar QR activos** desde su panel sin borrar el registro (queda en historial). |
| `Token` | El PDF exige que cada QR contenga un **token único firmado y verificado en el backend** — esto previene la suplantación: el QR en sí no es el dato sensible, lo es el token firmado dentro. |
| `UsedAt` | Momento exacto del primer uso; soporta auditoría y la regla *"imposibilidad de modificar un QR ya utilizado"*. |
| `CreatedAt` | Auditoría básica. |

---

## 5. `AccessEvent`

**Colección Firestore:** `accessevents`

**Log inmutable** de todo ingreso o salida. El PDF lo describe como *"registro inmutable
sin posibilidad de modificación"* — en código esto se garantiza al no exponer ningún
endpoint `PUT`/`DELETE` sobre esta colección y al aplicar reglas de seguridad en
Firestore que prohíban `update`/`delete`.

| Campo | Por qué |
|---|---|
| `UserId` | Si el que entra es un residente registrado, queda enlazado a su cuenta. Es `null` para visitantes. |
| `ResidentId` | El residente vinculado al evento (dueño del QR o de la casa). Necesario para los reportes por residente y para disparar la **notificación en tiempo real** al residente cuando un visitante suyo ingresa. |
| `VisitorName`, `VisitorIdNumber`, `VisitorVehiclePlate` | Para registros manuales el PDF pide capturar *nombre completo, número de identidad y placa del vehículo*. Estos campos viven en el evento (no en `Resident`) porque el visitante no es un residente del sistema. |
| `EvidencePhotoUrl` | URL en Firebase Storage `/visitors/evidence/` — el PDF pide **evidencia fotográfica** en registros manuales. |
| `EventType` (`entry` / `exit`) | El PDF distingue entrada/salida; permite calcular tiempos de permanencia y ocupación actual. |
| `AccessMethod` (`qr` / `manual`) | El PDF pide que *"todo registro manual quede marcado diferenciado en el log"* y que el dashboard muestre **% de accesos por tipo (QR / manual)**. |
| `QrId` | Trazabilidad: del evento puedo saltar al QR que se usó. `null` para registros manuales. |
| `GuardId` | El guardia que validó el QR o registró manualmente — auditoría por turno (*"consulta del historial de accesos del turno activo"*). |
| `Timestamp` | El PDF exige **registro de timestamp en cada evento de acceso**. Es la dimensión principal de los reportes de tendencia. |
| `Result` (`authorized` / `rejected`) | Tanto los aceptados como los rechazados se registran — el Escenario 2 pide *"acceso bloqueado, evento de rechazo registrado en el log"*. |
| `RejectionReason` | Detalla **por qué** se rechazó (QR vencido, ya usado, revocado, token inválido). Necesario para que el dashboard de admin muestre el estado real de cada evento. |

---

## 6. `AccessStatistics`

**No es una colección.** Es un DTO/modelo de salida que arma `ReportService` al consultar
Firestore. Existe para que el endpoint de reportes devuelva una única respuesta tipada
que el dashboard de Angular pueda consumir directamente con sus gráficos (Chart.js).

| Campo | Mapea a |
|---|---|
| `TotalActiveResidents`, `TodayAccessEvents` | "Total de residentes activos y eventos de acceso del día" (tarjetas resumen del dashboard). |
| `QrAccessPercent`, `ManualAccessPercent` | "Porcentaje de accesos por tipo (QR / manual)". |
| `VisitorCount`, `ResidentCount`, `ManualCount` | "Gráfico circular de distribución de perfiles". |
| `AuthorizedCount`, `RejectedCount` | "Gráfico de barras por tipo de acceso" + visibilidad de rechazos. |
| `Trend` (lista de `TimeSeriesPoint`) | "Gráfico de tendencia temporal de flujo de acceso" — por día/semana/mes según el filtro. |
| `PeriodStart`, `PeriodEnd` | Refleja el filtro aplicado, para que el frontend pueda mostrar el rango activo y validar el caché. |

---

## Relaciones (vista rápida)

```
User (1) ──── (0..1) Resident ──── (N) Vehicle
                       │
                       │ (1)
                       │
                      QRCode (N)
                       │
                       │ (1)
                       │
              AccessEvent (N) ──── (0..1) User (guardId)
```

- Un `User` puede tener cero o un `Resident` (admin y seguridad no tienen).
- Un `Resident` tiene N `Vehicle` (placa única en el sistema).
- Un `Resident` tiene N `QRCode` (1 permanente + N de visita).
- Un `QRCode` genera 0..N `AccessEvent` (permanente y largo plazo: múltiples; fecha: 1).
- Un `AccessEvent` referencia opcionalmente al `User` guardia que lo registró.

---

## Capa de Persistencia

Firestore **no tiene** `DbContext` ni Fluent API como EF Core (no existe `OnModelCreating`,
no hay `HasKey`/`HasForeignKey`, no hay migraciones). Lo más cercano que se mantiene en
este proyecto es una capa mínima en `Persistence/`:

```
Persistence/
├── CollectionNames.cs    ← strings de las colecciones, en un solo lugar
└── FirestoreContext.cs   ← "DbContext" — colecciones tipadas + transacciones
```

### `[FirestoreData]` / `[FirestoreProperty]` (≈ atributos de mapeo)

Cada modelo está anotado para que el SDK serialice automáticamente. Reglas que sigue el
proyecto:

- **`[FirestoreData]`** en la clase → declara que el tipo es persistible.
- **`[FirestoreProperty("nombreCamelCase")]`** en cada propiedad → fija el nombre exacto
  del campo en Firestore. Se usa **camelCase** (convención Firestore) aunque la propiedad
  C# sea PascalCase. Esto es importante: las queries por campo (`WhereEqualTo("email", ...)`)
  usan el nombre Firestore, no el nombre C#.
- **`DateTime` → `Timestamp`**: todos los campos de fecha usan `Google.Cloud.Firestore.Timestamp`
  (tipo nativo). `DateTime` también funciona pero exige que esté en UTC y abre la puerta
  a bugs de zona horaria; `Timestamp` lo elimina.

### `FirestoreContext` (≈ DbContext)

Expone una propiedad tipada por colección — el análogo de `DbSet<T>`:

```csharp
public CollectionReference Users => _db.Collection(CollectionNames.Users);
public CollectionReference Residents => _db.Collection(CollectionNames.Residents);
public CollectionReference Vehicles => _db.Collection(CollectionNames.Vehicles);
public CollectionReference QRCodes => _db.Collection(CollectionNames.QRCodes);
public CollectionReference AccessEvents => _db.Collection(CollectionNames.AccessEvents);
```

También expone `RunTransactionAsync(...)` para operaciones que tienen que ser atómicas
(p. ej. el requerimiento del PDF de **marcar QR como utilizado y crear el `AccessEvent`
en la misma transacción** para evitar reutilización).

Se registra en DI como `Scoped` y depende del `FirestoreDb` singleton que arma
`FirebaseService`:

```csharp
builder.Services.AddSingleton<FirebaseService>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<FirebaseService>().FirestoreDb);
builder.Services.AddScoped<FirestoreContext>();
```

### Lo que esta capa NO hace (limitaciones honestas)

- **No hay migraciones.** Firestore es schemaless: agregar un campo nuevo a un modelo
  funciona sin tocar la base. Los documentos viejos simplemente no tendrán el campo, y
  hay que tolerarlo en el código (defaults o nullables).
- **No hay change tracking.** Cada `SetAsync`/`UpdateAsync` es un round-trip directo a
  Firestore; no existe `SaveChanges()`.
- **No hay relaciones eager/lazy.** Las "FKs" (`UserId`, `ResidentId`, `QrId`...) son
  strings sin restricción de integridad referencial; hay que cargar manualmente lo que se
  quiera traer (o duplicar datos, que es el patrón NoSQL típico).
- **Los índices compuestos se crean fuera del código**, desde la consola de Firebase
  (o vía `firestore.indexes.json`). Cuando las consultas crezcan, Firestore va a tirar
  `FailedPrecondition` con un link directo para crear el índice que falta.

---

## Decisiones que conviene confirmar

1. **¿`User` y `Resident` comparten Id o son entidades separadas con `UserId` como FK?**
   Acá se asumió **separadas con FK**, porque admin/seguridad existen sin `Resident`. Si
   prefieres que el `Resident.Id` sea el mismo `User.Id`, se simplifica la lectura pero
   hay que validar que no se cree un `Resident` para un user no-residente.
2. **Datos del vehículo: ¿uno o varios por residente?** ✅ Resuelto — un residente puede
   tener N vehículos (colección `vehicles` top-level con `ResidentId` como FK).
   Pendiente confirmar: ¿hay un límite de vehículos por residencia configurable por el
   admin (similar al de QR permanentes)?
3. **`QrTypes.LongTerm`**: ¿tiene un `ExpiresAt` fijo configurable por admin (ej. 30 días)
   o lo define el residente al generarlo? Esto cambia la UI del `VisitQRGeneratorComponent`.
4. **Reglas de Firestore para inmutabilidad de `AccessEvents`**: deben configurarse en
   la consola de Firebase (no es código C#) para que el log sea *realmente* inmutable.
5. **`AccessStatistics`**: ¿se calcula en cada request, o se cachea/precalcula con un job
   programado? Para volúmenes bajos basta calcular on-the-fly; con miles de eventos
   diarios conviene una colección `dailyStats` precalculada.
