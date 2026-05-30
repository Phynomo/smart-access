using Google.Cloud.Firestore;
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

        public async Task<User> Login(string email, string password)
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

            return user;
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
    }
}
