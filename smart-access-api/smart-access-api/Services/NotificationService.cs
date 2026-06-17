using Google.Cloud.Firestore;
using smart_access_api.Models;
using smart_access_api.Persistence;

namespace smart_access_api.Services
{
    public class NotificationService
    {
        private readonly FirestoreContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(FirestoreContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Crea una notificación en tiempo real para el residente dueño del QR
        // cuando un visitante ingresa o sale usando su código.
        // Se llama fire-and-forget desde AccessService; los errores se loguean
        // pero no bloquean la respuesta al guardia.
        public async Task CreateForVisitAsync(
            AccessEvent ev,
            Resident resident,
            string qrType,
            string? visitorName)
        {
            if (string.IsNullOrWhiteSpace(resident.UserId))
                return;

            var name    = visitorName ?? "Visitante";
            var isEntry = ev.EventType == EventTypes.Entry;

            var notification = new Notification
            {
                Id           = Guid.NewGuid().ToString(),
                UserId       = resident.UserId,
                ResidentId   = resident.Id,
                Type         = isEntry ? NotificationTypes.VisitorArrived : NotificationTypes.VisitorDeparted,
                Title        = isEntry ? "Visitante en puerta" : "Visitante salió",
                Message      = isEntry
                    ? $"{name} acaba de ingresar con tu código QR."
                    : $"{name} acaba de salir.",
                VisitorName  = visitorName,
                AccessEventId = ev.Id,
                QrType       = qrType,
                IsRead       = false,
                CreatedAt    = Timestamp.FromDateTime(DateTime.UtcNow),
            };

            try
            {
                await _context.Notifications.Document(notification.Id).SetAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al crear notificación para residente {ResidentId}", resident.Id);
            }
        }

        // Lista las notificaciones del usuario, más recientes primero.
        // Se ordena en memoria para no exigir un índice compuesto en Firestore.
        public async Task<List<Notification>> GetForUserAsync(string userId, int limit = 50)
        {
            var snapshot = await _context.Notifications
                .WhereEqualTo("userId", userId)
                .GetSnapshotAsync();

            return snapshot.Documents
                .Select(d => d.ConvertTo<Notification>())
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToList();
        }

        // Marca una notificación como leída, validando que pertenezca al usuario.
        public async Task MarkAsReadAsync(string id, string userId)
        {
            var docRef = _context.Notifications.Document(id);
            var doc = await docRef.GetSnapshotAsync();
            if (!doc.Exists)
                return;

            var notification = doc.ConvertTo<Notification>();
            if (notification.UserId != userId)
                return; // No es del usuario: se ignora en silencio.

            await docRef.UpdateAsync("isRead", true);
        }

        // Marca todas las no leídas del usuario como leídas (batch).
        public async Task<int> MarkAllReadAsync(string userId)
        {
            var snapshot = await _context.Notifications
                .WhereEqualTo("userId", userId)
                .WhereEqualTo("isRead", false)
                .GetSnapshotAsync();

            if (snapshot.Count == 0)
                return 0;

            var batch = _context.Db.StartBatch();
            foreach (var doc in snapshot.Documents)
                batch.Update(doc.Reference, "isRead", true);

            await batch.CommitAsync();
            return snapshot.Count;
        }
    }
}
