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
    }
}
