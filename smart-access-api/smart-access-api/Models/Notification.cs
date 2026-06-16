using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    [FirestoreData]
    public class Notification
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        // userId del residente que recibe la notificación (para query en Firestore).
        [FirestoreProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [FirestoreProperty("residentId")]
        public string ResidentId { get; set; } = string.Empty;

        // NotificationTypes: visitor_arrived | visitor_departed
        [FirestoreProperty("type")]
        public string Type { get; set; } = string.Empty;

        [FirestoreProperty("title")]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty("message")]
        public string Message { get; set; } = string.Empty;

        [FirestoreProperty("visitorName")]
        public string? VisitorName { get; set; }

        // Referencia al AccessEvent que disparó esta notificación.
        [FirestoreProperty("accessEventId")]
        public string AccessEventId { get; set; } = string.Empty;

        [FirestoreProperty("qrType")]
        public string? QrType { get; set; }

        [FirestoreProperty("isRead")]
        public bool IsRead { get; set; } = false;

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.FromDateTime(DateTime.UtcNow);
    }
}
