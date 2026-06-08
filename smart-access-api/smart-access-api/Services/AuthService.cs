using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Cloud.Firestore;
using Microsoft.IdentityModel.Tokens;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Persistence;

namespace smart_access_api.Services
{
    public class AuthService
    {
        private readonly FirestoreContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(FirestoreContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // Login flexible: el identificador puede ser un correo o un número de casa.
        public async Task<LoginResponseDto> Login(string identifier, string password)
        {
            identifier = (identifier ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(identifier))
                throw BusinessException.BadRequest("Debes indicar tu correo o número de casa.");

            // Si parece correo, se busca por email; de lo contrario, por número de casa.
            var query = identifier.Contains('@')
                ? _context.Users.WhereEqualTo("email", identifier.ToLowerInvariant())
                : _context.Users.WhereEqualTo("houseNumber", identifier);

            var snapshot = await query.Limit(1).GetSnapshotAsync();
            if (snapshot.Count == 0)
                throw BusinessException.Unauthorized("Credenciales inválidas.");

            var user = snapshot.Documents[0].ConvertTo<User>();

            if (!user.IsActive)
                throw BusinessException.Forbidden("La cuenta está desactivada. Contacta al administrador.");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw BusinessException.Unauthorized("Credenciales inválidas.");

            return new LoginResponseDto
            {
                Token = GenerateToken(user),
                User = UserResponseDto.From(user),
            };
        }

        public async Task<User> Register(RegisterDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var existing = await _context.Users
                .WhereEqualTo("email", email)
                .Limit(1)
                .GetSnapshotAsync();

            if (existing.Count > 0)
                throw BusinessException.Conflict("Ya existe una cuenta con ese correo.");

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRoles.Resident,
                IsActive = true,
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
            };

            await _context.Users.Document(user.Id).SetAsync(user);
            return user;
        }

        public async Task<User?> GetById(string id)
        {
            var doc = await _context.Users.Document(id).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<User>() : null;
        }

        public async Task<List<User>> GetAll()
        {
            var snapshot = await _context.Users.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<User>()).ToList();
        }

        private string GenerateToken(User user)
        {
            // El token lleva Id, Email y Role del usuario; así los endpoints saben
            // quién llama y con qué rol.
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
