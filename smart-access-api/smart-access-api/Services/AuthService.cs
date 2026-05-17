using smart_access_api.DTOs;
using smart_access_api.Models;

namespace smart_access_api.Services
{
    public class AuthService
    {
        private readonly FirebaseService _firebaseService;
        private readonly IConfiguration _configuration;

        public AuthService(FirebaseService firebaseService, IConfiguration configuration)
        {
            _firebaseService = firebaseService;
            _configuration = configuration;
        }


        public async Task<User> Login(string email, string password)
        {
            var usersCollection = _firebaseService.GetCollection("users");
            var snapshot = await usersCollection
                .WhereEqualTo("Email", email)
                .Limit(1)
                .GetSnapshotAsync();

            if (snapshot.Count == 0)
                throw new Exception("Invalid credentials");

            var doc = snapshot.Documents[0];
            var user = new User
            {
                Id = doc.GetValue<string>("Id"),
                Name = doc.GetValue<string>("Name"),
                Email = doc.GetValue<string>("Email"),
                PasswordHash = doc.GetValue<string>("PasswordHash"),
                Role = doc.GetValue<string>("Role"),
                CreatedAt = doc.GetValue<DateTime>("CreatedAt")
            };

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new Exception("Invalid credentials");

            return user;
        }

        public async Task<User> Register(RegisterDto dto)
        {
            var usersCollection = _firebaseService.GetCollection("users");
            var existing = await usersCollection
                .WhereEqualTo("Email", dto.Email)
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
                Role = "user",
                CreatedAt = DateTime.UtcNow

            };

            await usersCollection.Document(user.Id).SetAsync(new Dictionary<string, object>
            {
                { "Id", user.Id },
                { "Name", user.Name },
                { "Email", user.Email },
                { "PasswordHash", user.PasswordHash },
                { "Role", user.Role },
                { "CreatedAt", user.CreatedAt }
            });

            return user;
        }

        public async Task<User> GetById(string id)
        {
            var doc = await _firebaseService.GetCollection("users").Document(id).GetSnapshotAsync();
            if (!doc.Exists)
                return null;

            return new User
            {
                Id = doc.GetValue<string>("Id"),
                Name = doc.GetValue<string>("Name"),
                Email = doc.GetValue<string>("Email"),
                PasswordHash = doc.GetValue<string>("PasswordHash"),
                Role = doc.GetValue<string>("Role"),
                CreatedAt = doc.GetValue<DateTime>("CreatedAt")
            };
        }

        public async Task<List<User>> GetAll()
        {
            var snapshot = await _firebaseService.GetCollection("users").GetSnapshotAsync();
            return snapshot.Documents.Select(doc => new User
            {
                Id = doc.GetValue<string>("Id"),
                Name = doc.GetValue<string>("Name"),
                Email = doc.GetValue<string>("Email"),
                PasswordHash = doc.GetValue<string>("PasswordHash"),
                Role = doc.GetValue<string>("Role"),
                CreatedAt = doc.GetValue<DateTime>("CreatedAt")
            }).ToList();
        }


    }
}
