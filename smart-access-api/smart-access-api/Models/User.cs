using Google.Cloud.Firestore;

namespace smart_access_api.Models
{
    [FirestoreData]
    public class User
    {
        [FirestoreProperty("id")]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        // Permite login flexible (correo o número de casa) — requerimiento del PDF.
        [FirestoreProperty("houseNumber")]
        public string HouseNumber { get; set; } = string.Empty;

        // UserRoles: Admin | Security | Resident
        [FirestoreProperty("role")]
        public string Role { get; set; } = UserRoles.Resident;

        // Referencia al QR permanente asignado al residente (null para admin/seguridad).
        [FirestoreProperty("qrPermanentId")]
        public string? QrPermanentId { get; set; }

        [FirestoreProperty("isActive")]
        public bool IsActive { get; set; } = true;

        // True cuando la cuenta se creó con una contraseña autogenerada: en el primer
        // cambio NO se pide la contraseña anterior. Se limpia tras cambiarla.
        [FirestoreProperty("mustChangePassword")]
        public bool MustChangePassword { get; set; } = false;

        // Token de un solo uso para el flujo de "olvidé mi contraseña" (self-service).
        // Null cuando no hay una solicitud de restablecimiento pendiente.
        [FirestoreProperty("resetToken")]
        public string? ResetToken { get; set; }

        [FirestoreProperty("resetTokenExpiresAt")]
        public Timestamp? ResetTokenExpiresAt { get; set; }

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; } = Timestamp.FromDateTime(DateTime.UtcNow);
    }
}
