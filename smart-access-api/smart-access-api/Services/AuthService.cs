using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
        private readonly EmailService _emailService;

        public AuthService(FirestoreContext context, IConfiguration configuration, EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        private string QrSecret =>
            _configuration["Qr:Key"] ?? _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta la clave de firma de QR (Qr:Key o Jwt:Key).");

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

        // Genera un token de un solo uso y envía por correo el enlace para que el
        // usuario defina una nueva contraseña. Por seguridad, no revela si el
        // identificador existe o no: siempre "termina bien" desde el punto de vista
        // del cliente, el correo solo se envía si se encontró una cuenta.
        public async Task ForgotPassword(string identifier)
        {
            identifier = (identifier ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(identifier))
                return;

            var query = identifier.Contains('@')
                ? _context.Users.WhereEqualTo("email", identifier.ToLowerInvariant())
                : _context.Users.WhereEqualTo("houseNumber", identifier);

            var snapshot = await query.Limit(1).GetSnapshotAsync();
            if (snapshot.Count == 0)
                return;

            var docRef = snapshot.Documents[0].Reference;
            var user = snapshot.Documents[0].ConvertTo<User>();
            if (!user.IsActive)
                return;

            var token = GenerateResetToken();
            user.ResetToken = token;
            user.ResetTokenExpiresAt = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(ResetTokenValidityMinutes));
            await docRef.SetAsync(user);

            var frontendUrl = (_configuration["App:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
            var resetUrl = $"{frontendUrl}/reset-password?token={token}";
            await _emailService.SendPasswordResetLinkAsync(user.Email, user.Name, resetUrl);
        }

        // Valida el token recibido por correo y establece la nueva contraseña.
        public async Task ResetPassword(string token, string newPassword)
        {
            token = (token ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(token))
                throw BusinessException.BadRequest("El enlace de restablecimiento no es válido.");

            var snapshot = await _context.Users.WhereEqualTo("resetToken", token).Limit(1).GetSnapshotAsync();
            if (snapshot.Count == 0)
                throw BusinessException.BadRequest("El enlace de restablecimiento no es válido o ya fue utilizado.");

            var docRef = snapshot.Documents[0].Reference;
            var user = snapshot.Documents[0].ConvertTo<User>();

            if (user.ResetTokenExpiresAt is null || user.ResetTokenExpiresAt.Value.ToDateTime() < DateTime.UtcNow)
                throw BusinessException.BadRequest("El enlace de restablecimiento venció. Solicita uno nuevo.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = false;
            user.ResetToken = null;
            user.ResetTokenExpiresAt = null;

            await docRef.SetAsync(user);
        }

        private const int ResetTokenValidityMinutes = 60;

        private static string GenerateResetToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // Cambia la contraseña del usuario autenticado. En el primer cambio (cuenta con
        // contraseña autogenerada, MustChangePassword=true) NO se exige la anterior.
        public async Task<User> ChangePassword(string userId, ChangePasswordDto dto)
        {
            var docRef = _context.Users.Document(userId);
            var doc = await docRef.GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Usuario no encontrado.");

            var user = doc.ConvertTo<User>();

            if (!user.MustChangePassword)
            {
                if (string.IsNullOrEmpty(dto.OldPassword))
                    throw BusinessException.BadRequest("Debes indicar tu contraseña actual.");
                if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
                    throw BusinessException.BadRequest("La contraseña actual no es correcta.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.MustChangePassword = false;

            await docRef.SetAsync(user);
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
            return snapshot.Documents
                .Select(d => d.ConvertTo<User>())
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToList();
        }

        // ── Operaciones de administración ────────────────────────────────────

        public async Task<(User user, string? plainPassword)> AdminCreate(AdminCreateUserDto dto, string adminId)
        {
            var role = dto.Role?.ToLowerInvariant();
            if (role != UserRoles.Admin && role != UserRoles.Security && role != UserRoles.Resident)
                throw BusinessException.BadRequest("Rol inválido. Usa: admin, security o resident.");

            if (role == UserRoles.Resident && string.IsNullOrWhiteSpace(dto.HouseNumber))
                throw BusinessException.BadRequest("El número de casa es obligatorio para residentes.");

            var email = dto.Email.Trim().ToLowerInvariant();
            var existing = await _context.Users.WhereEqualTo("email", email).Limit(1).GetSnapshotAsync();
            if (existing.Count > 0)
                throw BusinessException.Conflict("Ya existe una cuenta con ese correo.");

            string? plainPassword = null;
            string passwordHash;
            bool mustChange;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                mustChange = false;
            }
            else
            {
                plainPassword = GenerateTempPassword();
                passwordHash  = BCrypt.Net.BCrypt.HashPassword(plainPassword);
                mustChange    = true;
            }

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var houseNumber = dto.HouseNumber?.Trim() ?? string.Empty;

            var user = new User
            {
                Id                 = Guid.NewGuid().ToString(),
                Name               = dto.Name.Trim(),
                Email              = email,
                PasswordHash       = passwordHash,
                Role               = role,
                HouseNumber        = houseNumber,
                IsActive           = true,
                MustChangePassword = mustChange,
                CreatedAt          = now,
            };

            // Si el usuario es residente, se crea junto con su perfil Resident y su
            // QR permanente (de forma atómica), igual que ResidentService.Create.
            if (role == UserRoles.Resident)
            {
                var houseTaken = await _context.Residents
                    .WhereEqualTo("houseNumber", houseNumber)
                    .WhereEqualTo("isActive", true)
                    .Limit(1)
                    .GetSnapshotAsync();
                if (houseTaken.Count > 0)
                    throw BusinessException.Conflict("Ya existe un residente activo con ese número de casa.");

                var residentId = Guid.NewGuid().ToString();
                var permanentQrId = Guid.NewGuid().ToString();
                user.QrPermanentId = permanentQrId;

                var permanentQr = new QRCode
                {
                    Id = permanentQrId,
                    ResidentId = residentId,
                    VisitorName = null,
                    QrType = QrTypes.Permanent,
                    ValidDate = null,
                    ExpiresAt = null,
                    IsUsed = false,
                    IsRevoked = false,
                    Token = QrToken.Generate(permanentQrId, QrSecret),
                    CreatedAt = now,
                };

                var resident = new Resident
                {
                    Id = residentId,
                    UserId = user.Id,
                    Name = user.Name,
                    HouseNumber = houseNumber,
                    Email = email,
                    PhotoUrl = null,
                    ActivePermanentQrCount = 0,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = adminId,
                };

                var batch = _context.Db.StartBatch();
                batch.Set(_context.Users.Document(user.Id), user);
                batch.Set(_context.QRCodes.Document(permanentQrId), permanentQr);
                batch.Set(_context.Residents.Document(residentId), resident);
                await batch.CommitAsync();
            }
            else
            {
                await _context.Users.Document(user.Id).SetAsync(user);
            }

            return (user, plainPassword);
        }

        public async Task<User> AdminUpdate(string id, AdminUpdateUserDto dto)
        {
            var docRef = _context.Users.Document(id);
            var doc = await docRef.GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Usuario no encontrado.");

            var user = doc.ConvertTo<User>();

            // Validar unicidad de email si cambia
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var newEmail = dto.Email.Trim().ToLowerInvariant();
                if (newEmail != user.Email)
                {
                    var conflict = await _context.Users
                        .WhereEqualTo("email", newEmail).Limit(1).GetSnapshotAsync();
                    if (conflict.Count > 0)
                        throw BusinessException.Conflict("Ya existe una cuenta con ese correo.");
                    user.Email = newEmail;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
                user.Name = dto.Name.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Role))
            {
                var role = dto.Role.ToLowerInvariant();
                if (role != UserRoles.Admin && role != UserRoles.Security && role != UserRoles.Resident)
                    throw BusinessException.BadRequest("Rol inválido.");
                user.Role = role;
            }

            if (dto.HouseNumber is not null)
                user.HouseNumber = dto.HouseNumber.Trim();

            await docRef.SetAsync(user);
            return user;
        }

        public async Task<User> AdminSetActive(string id, bool active)
        {
            var docRef = _context.Users.Document(id);
            var doc = await docRef.GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Usuario no encontrado.");

            var user = doc.ConvertTo<User>();
            user.IsActive = active;
            await docRef.SetAsync(user);
            return user;
        }

        // Genera contraseña temporal y fuerza cambio en el próximo login.
        public async Task<(User user, string plainPassword)> AdminResetPassword(string id)
        {
            var docRef = _context.Users.Document(id);
            var doc = await docRef.GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Usuario no encontrado.");

            var plain = GenerateTempPassword();
            var user = doc.ConvertTo<User>();
            user.PasswordHash       = BCrypt.Net.BCrypt.HashPassword(plain);
            user.MustChangePassword = true;

            await docRef.SetAsync(user);
            return (user, plain);
        }

        private static string GenerateTempPassword()
        {
            const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
            var rng = Random.Shared;
            return new string(Enumerable.Range(0, 10).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
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
                expires: DateTime.UtcNow.AddDays(30),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
