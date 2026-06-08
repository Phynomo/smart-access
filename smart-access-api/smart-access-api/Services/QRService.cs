using Google.Cloud.Firestore;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Persistence;

namespace smart_access_api.Services
{
    public class QRService
    {
        private readonly FirestoreContext _context;
        private readonly IConfiguration _configuration;

        public QRService(FirestoreContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string QrSecret =>
            _configuration["Qr:Key"] ?? _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta la clave de firma de QR (Qr:Key o Jwt:Key).");

        private int MaxLongTermPerResident =>
            int.TryParse(_configuration["Residency:MaxLongTermQrPerResident"], out var v) ? v : 5;

        private int DefaultLongTermDays =>
            int.TryParse(_configuration["Residency:LongTermDefaultDays"], out var v) ? v : 30;

        // El residente (o el admin en su nombre) genera un QR de visita.
        public async Task<QRCode> GenerateVisitQr(string residentId, GenerateQrDto dto, string callerUserId, string callerRole)
        {
            var resident = await GetActiveResidentOrThrow(residentId);
            EnsureOwnership(resident, callerUserId, callerRole);

            return dto.QrType switch
            {
                QrTypes.Date => await GenerateDateQr(resident, dto),
                QrTypes.LongTerm => await GenerateLongTermQr(resident, dto),
                QrTypes.Permanent => throw BusinessException.BadRequest(
                    "El QR permanente no se genera manualmente; se asigna al crear el residente."),
                _ => throw BusinessException.BadRequest(
                    "Tipo de QR inválido. Use 'date' o 'long_term'."),
            };
        }

        private async Task<QRCode> GenerateDateQr(Resident resident, GenerateQrDto dto)
        {
            if (dto.ValidDate is null)
                throw BusinessException.BadRequest("El QR de visita por fecha requiere 'validDate'.");

            var day = DateTime.SpecifyKind(dto.ValidDate.Value.Date, DateTimeKind.Utc);
            if (day < DateTime.UtcNow.Date)
                throw BusinessException.BadRequest("No se puede generar un QR para una fecha pasada.");

            // Vence al finalizar el día indicado.
            var expiresAt = day.AddDays(1).AddSeconds(-1);

            var qrId = Guid.NewGuid().ToString();
            var qr = new QRCode
            {
                Id = qrId,
                ResidentId = resident.Id,
                VisitorName = dto.VisitorName,
                QrType = QrTypes.Date,
                ValidDate = Timestamp.FromDateTime(day),
                ExpiresAt = Timestamp.FromDateTime(expiresAt),
                IsUsed = false,
                IsRevoked = false,
                Token = QrToken.Generate(qrId, QrSecret),
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
            };

            await _context.QRCodes.Document(qrId).SetAsync(qr);
            return qr;
        }

        // El QR de larga duración cuenta contra el límite configurable por residencia.
        // Se crea dentro de una transacción para que el contador no se pase del límite
        // ante solicitudes concurrentes.
        private async Task<QRCode> GenerateLongTermQr(Resident resident, GenerateQrDto dto)
        {
            var validUntil = dto.ValidUntil is null
                ? DateTime.UtcNow.Date.AddDays(DefaultLongTermDays)
                : DateTime.SpecifyKind(dto.ValidUntil.Value.Date, DateTimeKind.Utc);

            if (validUntil <= DateTime.UtcNow.Date)
                throw BusinessException.BadRequest("La fecha de vencimiento debe ser futura.");

            var expiresAt = validUntil.AddDays(1).AddSeconds(-1);
            var qrId = Guid.NewGuid().ToString();
            var limit = MaxLongTermPerResident;

            var residentRef = _context.Residents.Document(resident.Id);
            var qrRef = _context.QRCodes.Document(qrId);

            return await _context.RunTransactionAsync(async tx =>
            {
                var resSnap = await tx.GetSnapshotAsync(residentRef);
                if (!resSnap.Exists)
                    throw BusinessException.NotFound("Residente no encontrado.");

                var current = resSnap.ConvertTo<Resident>();
                if (current.ActivePermanentQrCount >= limit)
                    throw BusinessException.Conflict(
                        $"Se alcanzó el límite de {limit} QR de larga duración activos para esta residencia.");

                var qr = new QRCode
                {
                    Id = qrId,
                    ResidentId = resident.Id,
                    VisitorName = dto.VisitorName,
                    QrType = QrTypes.LongTerm,
                    ValidDate = null,
                    ExpiresAt = Timestamp.FromDateTime(expiresAt),
                    IsUsed = false,
                    IsRevoked = false,
                    Token = QrToken.Generate(qrId, QrSecret),
                    CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                };

                tx.Update(residentRef, "activePermanentQrCount", current.ActivePermanentQrCount + 1);
                tx.Set(qrRef, qr);
                return qr;
            });
        }

        public async Task<QRCode?> GetPermanentForResident(string residentId)
        {
            var snapshot = await _context.QRCodes
                .WhereEqualTo("residentId", residentId)
                .WhereEqualTo("qrType", QrTypes.Permanent)
                .Limit(1)
                .GetSnapshotAsync();

            return snapshot.Count == 0 ? null : snapshot.Documents[0].ConvertTo<QRCode>();
        }

        public async Task<List<QRCode>> GetByResident(string residentId)
        {
            var snapshot = await _context.QRCodes
                .WhereEqualTo("residentId", residentId)
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(d => d.ConvertTo<QRCode>())
                .OrderByDescending(q => q.CreatedAt)
                .ToList();
        }

        public async Task<QRCode?> GetById(string id)
        {
            var doc = await _context.QRCodes.Document(id).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<QRCode>() : null;
        }

        // Revoca un QR de visita. El permanente no se puede revocar; un QR ya usado
        // tampoco se modifica.
        public async Task Revoke(string qrId, string callerUserId, string callerRole)
        {
            var qrRef = _context.QRCodes.Document(qrId);
            var snap = await qrRef.GetSnapshotAsync();
            if (!snap.Exists)
                throw BusinessException.NotFound("QR no encontrado.");

            var qr = snap.ConvertTo<QRCode>();
            var resident = await GetActiveResidentOrThrow(qr.ResidentId);
            EnsureOwnership(resident, callerUserId, callerRole);

            if (qr.QrType == QrTypes.Permanent)
                throw BusinessException.BadRequest("El QR permanente no se puede revocar.");
            if (qr.IsRevoked)
                throw BusinessException.BadRequest("El QR ya estaba revocado.");
            if (qr.IsUsed)
                throw BusinessException.BadRequest("No se puede revocar un QR que ya fue utilizado.");

            // Para long_term hay que liberar un cupo del contador, de forma atómica.
            if (qr.QrType == QrTypes.LongTerm)
            {
                var residentRef = _context.Residents.Document(qr.ResidentId);
                await _context.RunTransactionAsync(async tx =>
                {
                    var resSnap = await tx.GetSnapshotAsync(residentRef);
                    var qrSnap = await tx.GetSnapshotAsync(qrRef);
                    if (!qrSnap.Exists) throw BusinessException.NotFound("QR no encontrado.");

                    var count = resSnap.Exists ? resSnap.ConvertTo<Resident>().ActivePermanentQrCount : 0;
                    tx.Update(qrRef, "isRevoked", true);
                    tx.Update(residentRef, "activePermanentQrCount", Math.Max(0, count - 1));
                    return true;
                });
            }
            else
            {
                await qrRef.UpdateAsync("isRevoked", true);
            }
        }

        // ----- Helpers -----

        private async Task<Resident> GetActiveResidentOrThrow(string residentId)
        {
            var doc = await _context.Residents.Document(residentId).GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Residente no encontrado.");

            var resident = doc.ConvertTo<Resident>();
            if (!resident.IsActive)
                throw BusinessException.BadRequest("El residente está inactivo.");

            return resident;
        }

        private static void EnsureOwnership(Resident resident, string callerUserId, string callerRole)
        {
            if (callerRole == UserRoles.Admin)
                return;
            if (resident.UserId != callerUserId)
                throw BusinessException.Forbidden("Sólo puedes gestionar los QR de tu propia cuenta.");
        }
    }
}
