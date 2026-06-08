using System.ComponentModel.DataAnnotations;

namespace smart_access_api.DTOs
{
    public class LoginDto
    {
        // Login flexible: correo electrónico O número de casa.
        [Required(ErrorMessage = "Debes indicar tu correo o número de casa.")]
        public string Identifier { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Password { get; set; } = string.Empty;
    }
}
