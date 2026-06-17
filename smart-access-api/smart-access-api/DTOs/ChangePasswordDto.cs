using System.ComponentModel.DataAnnotations;

namespace smart_access_api.DTOs
{
    // Cambio de contraseña del usuario autenticado.
    public class ChangePasswordDto
    {
        // Contraseña actual. Obligatoria salvo en el primer cambio (cuenta con
        // contraseña autogenerada): en ese caso se ignora.
        public string? OldPassword { get; set; }

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
