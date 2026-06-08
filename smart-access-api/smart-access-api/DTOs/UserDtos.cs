using smart_access_api.Models;

namespace smart_access_api.DTOs
{
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
