using System.ComponentModel.DataAnnotations;
using smart_access_api.Models;

namespace smart_access_api.DTOs
{
    // Admin crea un usuario de cualquier rol.
    // Si no envía contraseña, se genera una temporal y MustChangePassword = true.
    public class AdminCreateUserDto
    {
        [Required, StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = UserRoles.Resident;

        // Solo obligatorio para residentes (permite login por número de casa).
        [StringLength(20)]
        public string? HouseNumber { get; set; }

        // Opcional: si no se envía, se genera una contraseña temporal.
        [StringLength(100, MinimumLength = 6)]
        public string? Password { get; set; }
    }

    // El usuario solicita un enlace para restablecer su contraseña (sin estar
    // autenticado). El identificador puede ser correo o número de casa, igual que
    // en el login.
    public class ForgotPasswordDto
    {
        [Required(ErrorMessage = "Indica tu correo o número de casa.")]
        public string Identifier { get; set; } = string.Empty;
    }

    // El usuario llega desde el enlace del correo con el token y define su nueva
    // contraseña.
    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "El enlace de restablecimiento no es válido.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string NewPassword { get; set; } = string.Empty;
    }

    // Admin edita datos de cualquier usuario.
    public class AdminUpdateUserDto
    {
        [StringLength(100, MinimumLength = 2)]
        public string? Name { get; set; }

        [EmailAddress, StringLength(200)]
        public string? Email { get; set; }

        public string? Role { get; set; }

        [StringLength(20)]
        public string? HouseNumber { get; set; }
    }

    // Respuesta de usuario SIN PasswordHash. Nunca se debe devolver el hash al cliente.
    public class UserResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string HouseNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? QrPermanentId { get; set; }
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime CreatedAt { get; set; }

        public static UserResponseDto From(User u) => new()
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            HouseNumber = u.HouseNumber,
            Role = u.Role,
            QrPermanentId = u.QrPermanentId,
            IsActive = u.IsActive,
            MustChangePassword = u.MustChangePassword,
            CreatedAt = u.CreatedAt.ToDateTime(),
        };
    }

    // Respuesta del login: el token JWT + los datos del usuario (para que el front
    // no tenga que decodificar el token para mostrar nombre/rol).
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public UserResponseDto User { get; set; } = new();
    }
}
