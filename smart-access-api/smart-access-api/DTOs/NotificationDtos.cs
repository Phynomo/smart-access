using smart_access_api.Models;

namespace smart_access_api.DTOs
{
    // Notificación tal como la consume el frontend (mismo shape que VisitorNotification).
    public class NotificationResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ResidentId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? VisitorName { get; set; }
        public string AccessEventId { get; set; } = string.Empty;
        public string? QrType { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public static NotificationResponseDto From(Notification n) => new()
        {
            Id            = n.Id,
            UserId        = n.UserId,
            ResidentId    = n.ResidentId,
            Type          = n.Type,
            Title         = n.Title,
            Message       = n.Message,
            VisitorName   = n.VisitorName,
            AccessEventId = n.AccessEventId,
            QrType        = n.QrType,
            IsRead        = n.IsRead,
            CreatedAt     = n.CreatedAt.ToDateTime(),
        };
    }
}
