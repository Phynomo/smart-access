using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    [FirestoreData]
    public class QRCode
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        // Residente dueño del QR (todo QR pertenece a un residente).
        [FirestoreProperty("residentId")]
        public string ResidentId { get; set; } = string.Empty;

        // Nombre del visitante (null si el QR es permanente del residente).
        [FirestoreProperty("visitorName")]
        public string? VisitorName { get; set; }

        // QrTypes: Permanent | Date | LongTerm
        [FirestoreProperty("qrType")]
        public string QrType { get; set; } = QrTypes.Date;

        // Fecha autorizada (solo aplica a QR tipo Date).
        [FirestoreProperty("validDate")]
        public Timestamp? ValidDate { get; set; }

        // Vencimiento absoluto. Para Permanent puede ser null o muy lejano.
        [FirestoreProperty("expiresAt")]
        public Timestamp? ExpiresAt { get; set; }

        [FirestoreProperty("isUsed")]
        public bool IsUsed { get; set; } = false;

        [FirestoreProperty("isRevoked")]
        public bool IsRevoked { get; set; } = false;

        // Token firmado único — se valida en backend para evitar suplantación.
        [FirestoreProperty("token")]
        public string Token { get; set; } = string.Empty;

        // Momento exacto del primer uso (para auditoría e invariante "no modificable").
        [FirestoreProperty("usedAt")]
        public Timestamp? UsedAt { get; set; }

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.FromDateTime(DateTime.UtcNow);
    }
}
