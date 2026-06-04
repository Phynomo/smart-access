using Google.Cloud.Firestore;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Persistence;

namespace smart_access_api.Services
{
    public class AccessService
    {
        private readonly FirestoreContext _context;
        private readonly IConfiguration _configuration;

        public AccessService(FirestoreContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string QrSecret =>
            _configuration["Qr:Key"] ?? _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta la clave de firma de QR (Qr:Key o Jwt:Key).");

        // Valida un QR escaneado por el guardia y registra el evento.
        // TODO el resultado (autorizado o rechazado) queda en el log inmutable.
        public async Task<ValidationResultDto> ValidateQr(ValidateQrDto dto, string guardId)
        {
            var eventType = NormalizeEventType(dto.EventType);

            // 1) Verificar la firma del token. Si no es válida, ni siquiera buscamos
            //    en la base: registramos un rechazo por token manipulado/ inválido.
            if (!QrToken.TryGetId(dto.Token, QrSecret, out var qrId))
            {
                var rejected = await RecordStandaloneRejection(
                    eventType, guardId, "Token de QR inválido o manipulado.");
                return BuildResult(rejected, resident: null);
            }

            var qrRef = _context.QRCodes.Document(qrId);
            var now = DateTime.UtcNow;

            // 2) Validar + marcar como usado + registrar evento, todo atómico.
            var (accessEvent, resident) = await _context.RunTransactionAsync(async tx =>
            {
                var qrSnap = await tx.GetSnapshotAsync(qrRef);

                if (!qrSnap.Exists)
                {
                    var notFound = BuildEvent(
                        residentId: string.Empty, visitorName: null, qrId: null,
                        eventType, guardId, AccessResults.Rejected, "El QR no existe.");
                    tx.Set(_context.AccessEvents.Document(notFound.Id), notFound);
                    return (notFound, (Resident?)null);
                }

                var qr = qrSnap.ConvertTo<QRCode>();

                // Leer el residente (para mostrar nombre/casa en la confirmación).
                var resSnap = await tx.GetSnapshotAsync(_context.Residents.Document(qr.ResidentId));
                var res = resSnap.Exists ? resSnap.ConvertTo<Resident>() : null;

                // Reglas de validación.
                string? reason = null;
                if (qr.IsRevoked)
                    reason = "QR revocado.";
                else if (res is { IsActive: false })
                    reason = "El residente está inactivo.";
                else if (qr.ExpiresAt is { } exp && exp.ToDateTime() < now)
                    reason = "QR vencido.";
                else if (qr.QrType == QrTypes.Date && qr.IsUsed)
                    reason = "QR ya utilizado.";

                var authorized = reason is null;

                // El QR de visita por fecha es de un solo uso: se marca atómicamente.
                if (authorized && qr.QrType == QrTypes.Date)
                {
                    tx.Update(qrRef, new Dictionary<string, object>
                    {
                        ["isUsed"] = true,
                        ["usedAt"] = Timestamp.FromDateTime(now),
                    });
                }

                var ev = BuildEvent(
                    residentId: qr.ResidentId,
                    visitorName: qr.VisitorName,
                    qrId: qr.Id,
                    eventType,
                    guardId,
                    authorized ? AccessResults.Authorized : AccessResults.Rejected,
                    reason);

                tx.Set(_context.AccessEvents.Document(ev.Id), ev);
                return (ev, res);
            });

            return BuildResult(accessEvent, resident);
        }

        // Registro manual hecho por el guardia (sin QR).
        public async Task<AccessEvent> RegisterManual(ManualEntryDto dto, string guardId)
        {
            var eventType = NormalizeEventType(dto.EventType);
            var hasResident = !string.IsNullOrWhiteSpace(dto.ResidentId);
            var hasVisitor = !string.IsNullOrWhiteSpace(dto.VisitorName);

            if (!hasResident && !hasVisitor)
                throw BusinessException.BadRequest(
                    "Debes indicar un residente (ResidentId) o los datos del visitante (VisitorName).");

            string residentId = string.Empty;
            string? userId = null;

            if (hasResident)
            {
                var doc = await _context.Residents.Document(dto.ResidentId!).GetSnapshotAsync();
                if (!doc.Exists)
                    throw BusinessException.NotFound("Residente no encontrado.");

                var resident = doc.ConvertTo<Resident>();
                if (!resident.IsActive)
                    throw BusinessException.BadRequest("El residente está inactivo.");

                residentId = resident.Id;
                userId = resident.UserId;
            }

            var ev = new AccessEvent
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                ResidentId = residentId,
                VisitorName = hasVisitor ? dto.VisitorName : null,
                VisitorIdNumber = dto.VisitorIdNumber,
                VisitorVehiclePlate = dto.VisitorVehiclePlate,
                EvidencePhotoUrl = dto.EvidencePhotoUrl,
                EventType = eventType,
                AccessMethod = AccessMethods.Manual,
                QrId = null,
                GuardId = guardId,
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                Result = AccessResults.Authorized,
                RejectionReason = null,
            };

            await _context.AccessEvents.Document(ev.Id).SetAsync(ev);
            return ev;
        }

        // Listado para el admin con filtros opcionales.
        // Se aplica una igualdad en Firestore (residentId, si viene) y el resto de
        // filtros en memoria para no exigir múltiples índices compuestos.
        public async Task<List<AccessEvent>> Query(AccessQueryDto filters)
        {
            Query query = _context.AccessEvents;
            if (!string.IsNullOrWhiteSpace(filters.ResidentId))
                query = query.WhereEqualTo("residentId", filters.ResidentId);

            var snapshot = await query.GetSnapshotAsync();
            var events = snapshot.Documents.Select(d => d.ConvertTo<AccessEvent>()).AsEnumerable();

            events = ApplyInMemoryFilters(events, filters);

            return events.OrderByDescending(e => e.Timestamp).ToList();
        }

        public async Task<List<AccessEvent>> GetByResident(string residentId)
        {
            var snapshot = await _context.AccessEvents
                .WhereEqualTo("residentId", residentId)
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(d => d.ConvertTo<AccessEvent>())
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        // Historial del turno del guardia (por defecto desde la medianoche de hoy UTC).
        public async Task<List<AccessEvent>> GetShiftLog(string guardId, DateTime? since = null)
        {
            var from = since ?? DateTime.UtcNow.Date;

            var snapshot = await _context.AccessEvents
                .WhereEqualTo("guardId", guardId)
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(d => d.ConvertTo<AccessEvent>())
                .Where(e => e.Timestamp.ToDateTime() >= from)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        // ----- Helpers -----

        private static IEnumerable<AccessEvent> ApplyInMemoryFilters(
            IEnumerable<AccessEvent> events, AccessQueryDto f)
        {
            if (f.From is { } from)
                events = events.Where(e => e.Timestamp.ToDateTime() >= from.ToUniversalTime());
            if (f.To is { } to)
                events = events.Where(e => e.Timestamp.ToDateTime() <= to.ToUniversalTime());
            if (!string.IsNullOrWhiteSpace(f.AccessMethod))
                events = events.Where(e => e.AccessMethod == f.AccessMethod);
            if (!string.IsNullOrWhiteSpace(f.EventType))
                events = events.Where(e => e.EventType == f.EventType);
            if (!string.IsNullOrWhiteSpace(f.Result))
                events = events.Where(e => e.Result == f.Result);
            return events;
        }

        private async Task<AccessEvent> RecordStandaloneRejection(
            string eventType, string guardId, string reason)
        {
            var ev = BuildEvent(string.Empty, null, null, eventType, guardId, AccessResults.Rejected, reason);
            await _context.AccessEvents.Document(ev.Id).SetAsync(ev);
            return ev;
        }

        private static AccessEvent BuildEvent(
            string residentId, string? visitorName, string? qrId,
            string eventType, string guardId, string result, string? reason) => new()
            {
                Id = Guid.NewGuid().ToString(),
                ResidentId = residentId,
                VisitorName = visitorName,
                QrId = qrId,
                EventType = eventType,
                AccessMethod = AccessMethods.Qr,
                GuardId = guardId,
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                Result = result,
                RejectionReason = reason,
            };

        private static ValidationResultDto BuildResult(AccessEvent ev, Resident? resident) => new()
        {
            Authorized = ev.Result == AccessResults.Authorized,
            RejectionReason = ev.RejectionReason,
            Event = AccessEventResponseDto.From(ev),
            ResidentName = resident?.Name,
            HouseNumber = resident?.HouseNumber,
            VisitorName = ev.VisitorName,
        };

        private static string NormalizeEventType(string? eventType) =>
            eventType == EventTypes.Exit ? EventTypes.Exit : EventTypes.Entry;
    }
}
