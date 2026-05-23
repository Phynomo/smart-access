using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    [FirestoreData]
    public class AccessEvent
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        // Si el que ingresa es un usuario registrado (residente).
        [FirestoreProperty("userId")]
        public string? UserId { get; set; }

        // Residente vinculado al evento (dueño del QR o de la casa).
        [FirestoreProperty("residentId")]
        public string ResidentId { get; set; } = string.Empty;

        // Datos del visitante (aplica a registros manuales o QR de visita).
        [FirestoreProperty("visitorName")]
        public string? VisitorName { get; set; }

        [FirestoreProperty("visitorIdNumber")]
        public string? VisitorIdNumber { get; set; }

        [FirestoreProperty("visitorVehiclePlate")]
        public string? VisitorVehiclePlate { get; set; }

        // Evidencia fotográfica para registros manuales (Firebase Storage:
        // /visitors/evidence/{eventId}).
        [FirestoreProperty("evidencePhotoUrl")]
        public string? EvidencePhotoUrl { get; set; }

        // EventTypes: Entry | Exit
        [FirestoreProperty("eventType")]
        public string EventType { get; set; } = EventTypes.Entry;

        // AccessMethods: Qr | Manual
        [FirestoreProperty("accessMethod")]
        public string AccessMethod { get; set; } = AccessMethods.Qr;

        // QR usado (null si fue registro manual).
        [FirestoreProperty("qrId")]
        public string? QrId { get; set; }

        // Guardia que validó o registró (null si fue auto-validación de QR sin
        // intervención humana).
        [FirestoreProperty("guardId")]
        public string? GuardId { get; set; }

        [FirestoreProperty("timestamp")]
        public Timestamp Timestamp { get; set; } = Google.Cloud.Firestore.Timestamp.FromDateTime(DateTime.UtcNow);

        // AccessResults: Authorized | Rejected
        [FirestoreProperty("result")]
        public string Result { get; set; } = AccessResults.Authorized;

        // Detalle del rechazo (QR vencido, ya utilizado, revocado, etc.).
        [FirestoreProperty("rejectionReason")]
        public string? RejectionReason { get; set; }
    }
}
