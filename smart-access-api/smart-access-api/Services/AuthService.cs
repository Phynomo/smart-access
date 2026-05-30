using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Cloud.Firestore;
using Microsoft.IdentityModel.Tokens;
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

        public async Task<string> Login(string email, string password)
        {
            // Nota: los nombres de campo aquí ahora son camelCase porque los
            // modelos están anotados con [FirestoreProperty("email")], etc.
            var snapshot = await _context.Users
                .WhereEqualTo("email", email)
                .Limit(1)
                .GetSnapshotAsync();

            if (snapshot.Count == 0)
                throw new Exception("Invalid credentials");

            var user = snapshot.Documents[0].ConvertTo<User>();

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new Exception("Invalid credentials");

            return GenerateToken(user); ;
        }

        public async Task<User> Register(RegisterDto dto)
        {
            var existing = await _context.Users
                .WhereEqualTo("email", dto.Email)
                .Limit(1)
                .GetSnapshotAsync();

            if (existing.Count > 0)
                throw new Exception("User already exists");

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Email = dto.Email,
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
            // El token lleva cierta informacion, Id, Email y Role del usuario que hizo login
            // Para proteccion de los endpoints, se sabe quien los esta llamando
            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(

                    issuer: _configuration["Jwt:Issuer"], //Quien lo genera, nuestro token lo genera la app
                    audience: _configuration["Jwt:Issuer"], // Para quien lo genera, clientes / front-end
                    claims: claims, // Estos son los datos del usuario
                    expires: DateTime.UtcNow.AddHours(8), //Tiempo de vida del token
                    signingCredentials: creds // Firma de seguridad
                    );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
