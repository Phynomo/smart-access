using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    [FirestoreData]
    public class Resident
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        // Vinculación con User (mismo Id o referencia, según política del AuthService).
        [FirestoreProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("houseNumber")]
        public string HouseNumber { get; set; } = string.Empty;

        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;

        // Los vehículos viven en la colección `vehicles` con ResidentId como FK
        // (un residente puede tener N vehículos). Ver Vehicle.cs.

        // URL en Firebase Storage: /residents/photos/{residentId}.
        [FirestoreProperty("photoUrl")]
        public string? PhotoUrl { get; set; }

        // Contador de QR permanentes activos (límite configurable por admin).
        [FirestoreProperty("activePermanentQrCount")]
        public int ActivePermanentQrCount { get; set; } = 0;

        // Permite desactivar sin borrar historial.
        [FirestoreProperty("isActive")]
        public bool IsActive { get; set; } = true;

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.FromDateTime(DateTime.UtcNow);

        // Id del admin que creó el registro (auditoría).
        [FirestoreProperty("createdBy")]
        public string CreatedBy { get; set; } = string.Empty;
    }
}
