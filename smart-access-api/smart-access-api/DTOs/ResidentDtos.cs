using System.ComponentModel.DataAnnotations;
using smart_access_api.Models;

namespace smart_access_api.DTOs
{
    // Datos que el ADMIN envía al crear un residente.
    // El servidor crea: la cuenta User (rol resident), el perfil Resident, el QR
    // permanente y opcionalmente los vehículos. Id/CreatedAt/CreatedBy son del servidor.
    public class ResidentCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(120, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número de casa es obligatorio.")]
        [StringLength(20)]
        public string HouseNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string Email { get; set; } = string.Empty;

        // Contraseña inicial para que el residente pueda iniciar sesión.
        [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Password { get; set; } = string.Empty;

        public string? PhotoUrl { get; set; }

        // Vehículos opcionales a registrar junto con el residente (PDF: "datos del vehículo").
        public List<VehicleCreateDto>? Vehicles { get; set; }
    }

    // Datos editables de un residente. No incluye contraseña (eso es otro flujo).
    public class ResidentUpdateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(120, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número de casa es obligatorio.")]
        [StringLength(20)]
        public string HouseNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
        public string Email { get; set; } = string.Empty;

        public string? PhotoUrl { get; set; }
    }

    public class ResidentResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string HouseNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public int ActivePermanentQrCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;

        public static ResidentResponseDto From(Resident r) => new()
        {
            Id = r.Id,
            UserId = r.UserId,
            Name = r.Name,
            HouseNumber = r.HouseNumber,
            Email = r.Email,
            PhotoUrl = r.PhotoUrl,
            ActivePermanentQrCount = r.ActivePermanentQrCount,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt.ToDateTime(),
            CreatedBy = r.CreatedBy,
        };
    }
}
