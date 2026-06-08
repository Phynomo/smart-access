# ResidentPass — Reglas de negocio y API

Este documento explica las **reglas de negocio** implementadas en el backend y el
**contrato de la API** para integrar el frontend (Angular). Acompaña a
`Models/MODELS.md` (que describe los datos).

---

## 1. Formato de respuesta estándar (`ApiResponse<T>`)

**TODOS** los endpoints responden con la misma forma. El frontend puede tener un único
interceptor que la entienda:

```jsonc
{
  "success": true,            // ¿la operación fue exitosa?
  "code": 200,                // status HTTP (también es el status real de la respuesta)
  "message": "Operación realizada con éxito.",  // mensaje listo para mostrar
  "data": { /* ... */ },      // la información (null en errores)
  "errors": null              // lista de errores de detalle (validación), o null
}
```

- **Éxito**: `success: true`, `data` con el recurso.
- **Error de negocio** (404, 409, 403, 400): `success: false`, `message` explica el porqué.
- **Error de validación** (400): `errors` trae la lista de mensajes por campo.
- **Error inesperado** (500): `message` genérico (no se filtra el detalle interno).

> Implementado con `Common/ApiResponse.cs`, `Common/BusinessException.cs` y
> `Common/GlobalExceptionHandler.cs` (IExceptionHandler). Los controllers son delgados:
> los servicios lanzan `BusinessException` y el handler global la convierte en `ApiResponse`.

**Nota:** un `401` por token ausente/expirado lo emite el middleware de autenticación
*antes* de llegar al controller, por lo que ese caso puntual no lleva cuerpo `ApiResponse`.
El frontend debe tratar cualquier `401` como "ir a login".

---

## 2. Roles y autorización

Tres roles (claim `role` dentro del JWT): `admin`, `security`, `resident`.

La autorización se aplica en el **borde del controller** con `[Authorize(Roles = ...)]`
(además de las validaciones de pertenencia dentro de los servicios).

| Recurso / acción                         | admin | security | resident |
|------------------------------------------|:-----:|:--------:|:--------:|
| Crear/editar/desactivar residentes       |  ✅   |    ❌    |    ❌    |
| Listar todos los residentes              |  ✅   |    ❌    |    ❌    |
| Ver mi perfil de residente               |  ❌   |    ❌    |    ✅    |
| Gestionar vehículos de cualquier residente |  ✅   |    ❌    |    ❌    |
| Gestionar mis propios vehículos          |  ❌   |    ❌    |    ✅    |
| Generar/revocar mis QR                   |  ❌   |    ❌    |    ✅    |
| Generar QR en nombre de un residente     |  ✅   |    ❌    |    ❌    |
| Validar QR (escanear)                    |  ❌   |    ✅    |    ❌    |
| Registro manual de ingreso               |  ❌   |    ✅    |    ❌    |
| Ver log completo de accesos              |  ✅   |    ❌    |    ❌    |
| Ver mi historial de accesos              |  ❌   |    ❌    |    ✅    |
| Ver historial de mi turno                |  ❌   |    ✅    |    ❌    |
| Dashboard / reportes                     |  ✅   |    ❌    |    ❌    |

---

## 3. Endpoints

Base: `/api`. Todos requieren `Authorization: Bearer <token>` salvo los marcados *(público)*.

### Auth — `/api/auth`
| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/login` | *(público)* | Login flexible: `identifier` = correo **o** número de casa. Devuelve `{ token, user }`. |
| POST | `/register` | *(público)* | Auto-registro (rol resident por defecto). |
| GET | `/{id}` | admin | Usuario por id. |
| GET | `/` | admin | Lista de usuarios. |

### Residentes — `/api/residents`
| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/` | admin | Crea residente + cuenta de login + QR permanente + vehículos opcionales. |
| PUT | `/{id}` | admin | Edita datos del residente. |
| DELETE | `/{id}` | admin | **Desactiva** (soft delete), no borra. |
| GET | `/` | admin | Lista (filtro `?onlyActive=true`). |
| GET | `/{id}` | admin | Detalle. |
| GET | `/me` | resident | Mi propio perfil. |

### Vehículos — `/api/vehicles`
| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/resident/{residentId}` | admin | Registra vehículo a un residente. |
| GET | `/resident/{residentId}` | admin | Vehículos de un residente. |
| POST | `/mine` | resident | Registra uno de mis vehículos. |
| GET | `/mine` | resident | Mis vehículos. |
| PUT | `/{id}` | admin, resident | Edita (valida pertenencia). |
| DELETE | `/{id}` | admin, resident | Baja lógica (valida pertenencia). |

### Códigos QR — `/api/qrcodes`
| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| GET | `/mine/permanent` | resident | Mi QR permanente. |
| GET | `/mine` | resident | Todos mis QR. |
| POST | `/mine` | resident | Genera QR de visita (`date` o `long_term`). |
| POST | `/resident/{residentId}` | admin | Genera QR en nombre de un residente. |
| GET | `/resident/{residentId}` | admin | QR de un residente. |
| DELETE | `/{id}` | admin, resident | Revoca un QR (valida pertenencia). |

### Accesos — `/api/access`
| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| POST | `/validate` | security | Valida un QR escaneado y registra el evento. |
| POST | `/manual` | security | Registro manual (residente o visitante). |
| GET | `/` | admin | Log completo con filtros (`from,to,accessMethod,eventType,result,residentId`). |
| GET | `/mine` | resident | Mi historial de accesos. |
| GET | `/shift` | security | Historial de mi turno (`?since=`). |

### Reportes — `/api/reports`
| Método | Ruta | Rol | Descripción |
|---|---|---|---|
| GET | `/statistics` | admin | Agregados del dashboard (`?from&to&granularity=day|week|month`). |

---

## 4. Reglas de negocio por entidad

### Autenticación
- **Login flexible**: si el `identifier` contiene `@` se busca por correo; si no, por número de casa.
- Se rechaza el login de cuentas **inactivas** (`isActive=false`).
- Contraseñas hasheadas con **BCrypt**; nunca se devuelve el hash (`UserResponseDto`).
- Token JWT con expiración de 8 horas; claims: id, email, role.

### Residentes
- Crear un residente es una operación **atómica** (`WriteBatch`): crea la cuenta `User`
  (rol resident), el perfil `Resident`, su **QR permanente** y los vehículos opcionales.
- `email` único entre usuarios; `houseNumber` único entre residentes activos.
- **Soft delete**: desactivar pone `isActive=false` en el residente y en su cuenta de
  login (para impedir el acceso), preservando todo el historial.
- Editar el residente sincroniza nombre/correo/casa con su cuenta `User`.

### Vehículos
- Un residente puede tener **varios vehículos**.
- La **placa** se normaliza (mayúsculas, sin espacios ni guiones) y es única entre
  vehículos **activos**.
- Pertenencia: un residente sólo gestiona sus propios vehículos; el admin, cualquiera.
- Baja lógica (no se elimina) para no romper los eventos históricos que la referencian.

### Códigos QR
- Tipos: `permanent` (1 por residente, **automático**, no vence, no se revoca),
  `date` (visita por fecha) y `long_term` (visita recurrente).
- Cada QR lleva un **token firmado** (`HMACSHA256`) verificado en el backend → previene
  suplantación. El token es lo que el frontend codifica dentro de la imagen QR.
- **QR `date`**: requiere `validDate`; vence al finalizar ese día; es de **un solo uso**.
- **QR `long_term`**: cuenta contra un **límite configurable por residencia**
  (`Residency:MaxLongTermQrPerResident`, default 5). El contador se actualiza dentro de
  una **transacción** para no pasarse del límite con solicitudes concurrentes.
- No se puede **revocar** un QR permanente ni uno ya **utilizado**.

### Accesos (log inmutable)
- **Validación de QR** (`/validate`) corre en una **transacción de Firestore**:
  1. Verifica la firma del token (si es inválida → rechazo, sin tocar la base).
  2. Carga el QR y valida: no revocado, residente activo, no vencido, y (si es `date`) no usado.
  3. Si es válido y de tipo `date`, lo marca **atómicamente** como usado (`isUsed`, `usedAt`).
  4. Registra el `AccessEvent` (autorizado **o** rechazado) — siempre queda traza.
- `/validate` siempre responde **200**: la validación se ejecutó; el veredicto
  (`authorized` true/false + `rejectionReason`) va en el `data`.
- **Registro manual**: requiere `ResidentId` (residente conocido) **o** `VisitorName`
  (visitante, con identidad/placa/evidencia). Queda marcado `accessMethod=manual`.
- Los eventos **no** tienen endpoints de edición/borrado (log inmutable). Para que sea
  inmutable de verdad, configura reglas de Firestore que prohíban `update`/`delete` en
  `accessevents`.

### Reportes
- Agregados calculados al vuelo a partir de `accessevents` + `residents`.
- Distribución de perfiles: **residente** (QR sin visitante), **visitante** (QR con
  visitante) y **manual**.
- Serie de tendencia agrupada por día / semana / mes según `granularity`.

---

## 5. Decisiones tomadas (ajustables)

1. **Crear residente = crear también su cuenta de login.** El admin envía una contraseña
   inicial en el `POST /residents`. Alternativa: generar contraseña temporal y enviarla por
   correo (requiere servicio de email).
2. **El límite "por residencia" se aplica a los QR `long_term`** (interpretación del PDF,
   que mezcla "permanentes" y "larga duración"). El QR permanente propio no cuenta.
3. **Filtros de log en memoria.** Para no exigir múltiples índices compuestos de Firestore,
   el log se trae con una sola igualdad y el resto de filtros se aplican en memoria. Con
   volúmenes altos conviene mover a índices + paginación.
4. **Notificación en tiempo real al residente**: el backend la habilita simplemente
   *escribiendo el `AccessEvent`*. El frontend (Angular + Firebase SDK) debe **escuchar**
   la colección `accessevents` filtrada por su `residentId`. Si se quiere push del lado
   servidor, falta implementar `NotificationService` (FCM).
5. **Subida de imágenes** (foto de residente, evidencia de visitante): el backend recibe la
   **URL** ya subida a Firebase Storage; la subida en sí la hace el frontend con el SDK.
6. **Configuración del límite**: hoy vive en `appsettings.json` (`Residency:*`). Para que
   sea editable por el admin en runtime, habría que guardarlo en una colección `settings`.
