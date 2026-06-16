using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Persistence;

namespace smart_access_api.Services
{
    public class ResidentService
    {
        private readonly FirestoreContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public ResidentService(
            FirestoreContext context,
            IConfiguration configuration,
            EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        private string QrSecret =>
            _configuration["Qr:Key"] ?? _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta la clave de firma de QR (Qr:Key o Jwt:Key).");

        // Crea, de forma atómica (WriteBatch): la cuenta User (rol resident) —sólo si
        // no existe ya una con ese correo—, el perfil Resident, el QR permanente del
        // residente y los vehículos opcionales. Si se crea una cuenta nueva, se le
        // envía un correo con sus credenciales.
        public async Task<Resident> Create(ResidentCreateDto dto, string adminId)
        {
            await EnsureHouseNumberIsFree(dto.HouseNumber);

            var email = dto.Email.Trim().ToLowerInvariant();
            var residentId = Guid.NewGuid().ToString();
            var permanentQrId = Guid.NewGuid().ToString();
            var now = Timestamp.FromDateTime(DateTime.UtcNow);

            // ¿Ya existe un usuario con ese correo? Si sí, se reutiliza esa cuenta; si
            // no, se crea una nueva con rol resident y se le enviará la credencial.
            var existingUser = await FindUserByEmail(email);
            var createdNewUser = existingUser is null;
            var userId = existingUser?.Id ?? Guid.NewGuid().ToString();

            var batch = _context.Db.StartBatch();

            if (createdNewUser)
            {
                var user = new User
                {
                    Id = userId,
                    Name = dto.Name,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    HouseNumber = dto.HouseNumber.Trim(),
                    Role = UserRoles.Resident,
                    QrPermanentId = permanentQrId,
                    IsActive = true,
                    // Clave autogenerada: en su primer acceso deberá cambiarla.
                    MustChangePassword = true,
                    CreatedAt = now,
                };
                batch.Set(_context.Users.Document(userId), user);
            }
            else
            {
                // La cuenta ya existe: no debe estar ligada a otro residente.
                if (await GetByUserId(userId) is not null)
                    throw BusinessException.Conflict("Ya existe un residente asociado a esa cuenta.");

                // Apunta su QR permanente al recién creado y asegura número de casa.
                batch.Update(_context.Users.Document(userId), new Dictionary<string, object>
                {
                    ["qrPermanentId"] = permanentQrId,
                    ["houseNumber"] = dto.HouseNumber.Trim(),
                });
            }

            var permanentQr = new QRCode
            {
                Id = permanentQrId,
                ResidentId = residentId,
                VisitorName = null,
                QrType = QrTypes.Permanent,
                ValidDate = null,
                ExpiresAt = null, // el QR permanente no vence
                IsUsed = false,
                IsRevoked = false,
                Token = QrToken.Generate(permanentQrId, QrSecret),
                CreatedAt = now,
            };

            var resident = new Resident
            {
                Id = residentId,
                UserId = userId,
                Name = dto.Name,
                HouseNumber = dto.HouseNumber.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                PhotoUrl = dto.PhotoUrl,
                ActivePermanentQrCount = 0,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = adminId,
            };

            batch.Set(_context.QRCodes.Document(permanentQrId), permanentQr);
            batch.Set(_context.Residents.Document(residentId), resident);

            // Vehículos opcionales: validar placas (únicas y sin repetir en el payload).
            if (dto.Vehicles is { Count: > 0 })
            {
                var seenPlates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var v in dto.Vehicles)
                {
                    var plate = NormalizePlate(v.Plate);
                    if (!seenPlates.Add(plate))
                        throw BusinessException.Conflict($"La placa '{plate}' está repetida en la solicitud.");

                    await EnsurePlateIsFree(plate);

                    var vehicleId = Guid.NewGuid().ToString();
                    var vehicle = new Vehicle
                    {
                        Id = vehicleId,
                        ResidentId = residentId,
                        Plate = plate,
                        Brand = v.Brand,
                        Model = v.Model,
                        Color = v.Color,
                        Year = v.Year,
                        IsActive = true,
                        CreatedAt = now,
                    };
                    batch.Set(_context.Vehicles.Document(vehicleId), vehicle);
                }
            }

            await batch.CommitAsync();

            // Sólo si se creó una cuenta nueva tiene sentido enviar la credencial
            // (de la cuenta reutilizada no conocemos la contraseña). Best-effort.
            if (createdNewUser)
                await _emailService.SendResidentWelcomeAsync(email, dto.Name, email, dto.Password);

            return resident;
        }

        public async Task<Resident> Update(string id, ResidentUpdateDto dto)
        {
            var doc = await _context.Residents.Document(id).GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Residente no encontrado.");

            var resident = doc.ConvertTo<Resident>();

            var newEmail = dto.Email.Trim().ToLowerInvariant();
            var newHouse = dto.HouseNumber.Trim();

            if (!string.Equals(resident.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                await EnsureEmailIsFree(newEmail);
            if (!string.Equals(resident.HouseNumber, newHouse, StringComparison.OrdinalIgnoreCase))
                await EnsureHouseNumberIsFree(newHouse);

            resident.Name = dto.Name;
            resident.Email = newEmail;
            resident.HouseNumber = newHouse;
            resident.PhotoUrl = dto.PhotoUrl;

            var batch = _context.Db.StartBatch();
            batch.Set(_context.Residents.Document(id), resident);

            // Mantener sincronizada la cuenta de login (email/casa/nombre).
            if (!string.IsNullOrEmpty(resident.UserId))
            {
                batch.Update(_context.Users.Document(resident.UserId), new Dictionary<string, object>
                {
                    ["name"] = dto.Name,
                    ["email"] = newEmail,
                    ["houseNumber"] = newHouse,
                });
            }

            await batch.CommitAsync();
            return resident;
        }

        // Restablece la contraseña del residente: genera una temporal, marca
        // MustChangePassword y la envía por correo. El admin no la ve.
        public async Task ResetPassword(string residentId)
        {
            var resident = await GetById(residentId);
            if (resident is null)
                throw BusinessException.NotFound("Residente no encontrado.");

            if (string.IsNullOrEmpty(resident.UserId))
                throw BusinessException.BadRequest("El residente no tiene una cuenta de login asociada.");

            var userRef = _context.Users.Document(resident.UserId);
            var userDoc = await userRef.GetSnapshotAsync();
            if (!userDoc.Exists)
                throw BusinessException.NotFound("La cuenta del residente no existe.");

            var user = userDoc.ConvertTo<User>();
            var tempPassword = GenerateTempPassword();

            await userRef.UpdateAsync(new Dictionary<string, object>
            {
                ["passwordHash"] = BCrypt.Net.BCrypt.HashPassword(tempPassword),
                ["mustChangePassword"] = true,
            });

            await _emailService.SendPasswordResetAsync(user.Email, user.Name, user.Email, tempPassword);
        }

        // Desactivación lógica: NO borra el residente ni su historial. También
        // desactiva la cuenta de login para impedir el acceso.
        public async Task Deactivate(string id)
        {
            var doc = await _context.Residents.Document(id).GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Residente no encontrado.");

            var resident = doc.ConvertTo<Resident>();

            var batch = _context.Db.StartBatch();
            batch.Update(_context.Residents.Document(id), "isActive", false);
            if (!string.IsNullOrEmpty(resident.UserId))
                batch.Update(_context.Users.Document(resident.UserId), "isActive", false);

            await batch.CommitAsync();
        }

        // Reactiva un residente dado de baja (y su cuenta de login). Verifica que su
        // número de casa no esté ya ocupado por otro residente activo.
        public async Task Reactivate(string id)
        {
            var doc = await _context.Residents.Document(id).GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Residente no encontrado.");

            var resident = doc.ConvertTo<Resident>();
            if (resident.IsActive)
                return; // ya está activo, no hay nada que hacer

            var conflict = await _context.Residents
                .WhereEqualTo("houseNumber", resident.HouseNumber)
                .WhereEqualTo("isActive", true)
                .Limit(1)
                .GetSnapshotAsync();
            if (conflict.Count > 0)
                throw BusinessException.Conflict(
                    "Ya existe un residente activo con ese número de casa. Edita la casa antes de reactivar.");

            var batch = _context.Db.StartBatch();
            batch.Update(_context.Residents.Document(id), "isActive", true);
            if (!string.IsNullOrEmpty(resident.UserId))
                batch.Update(_context.Users.Document(resident.UserId), "isActive", true);

            await batch.CommitAsync();
        }

        public async Task<List<Resident>> GetAll(bool? onlyActive = null)
        {
            Query query = _context.Residents;
            if (onlyActive == true)
                query = query.WhereEqualTo("isActive", true);

            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents
                .Select(d => d.ConvertTo<Resident>())
                .OrderBy(r => r.Name)
                .ToList();
        }

        public async Task<Resident?> GetById(string id)
        {
            var doc = await _context.Residents.Document(id).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Resident>() : null;
        }

        public async Task<Resident?> GetByHouseNumber(string houseNumber)
        {
            var snapshot = await _context.Residents
                .WhereEqualTo("houseNumber", houseNumber.Trim())
                .WhereEqualTo("isActive", true)
                .Limit(1)
                .GetSnapshotAsync();

            return snapshot.Count == 0 ? null : snapshot.Documents[0].ConvertTo<Resident>();
        }

        public async Task<Resident?> GetByUserId(string userId)
        {
            var snapshot = await _context.Residents
                .WhereEqualTo("userId", userId)
                .Limit(1)
                .GetSnapshotAsync();

            return snapshot.Count == 0 ? null : snapshot.Documents[0].ConvertTo<Resident>();
        }

        // ----- Validaciones de unicidad -----

        // Devuelve el usuario con ese correo, o null si no existe.
        private async Task<User?> FindUserByEmail(string email)
        {
            var snapshot = await _context.Users
                .WhereEqualTo("email", email.Trim().ToLowerInvariant())
                .Limit(1)
                .GetSnapshotAsync();

            return snapshot.Count == 0 ? null : snapshot.Documents[0].ConvertTo<User>();
        }

        private async Task EnsureEmailIsFree(string email)
        {
            var snapshot = await _context.Users
                .WhereEqualTo("email", email.Trim().ToLowerInvariant())
                .Limit(1)
                .GetSnapshotAsync();

            if (snapshot.Count > 0)
                throw BusinessException.Conflict("Ya existe una cuenta con ese correo.");
        }

        private async Task EnsureHouseNumberIsFree(string houseNumber)
        {
            var snapshot = await _context.Residents
                .WhereEqualTo("houseNumber", houseNumber.Trim())
                .WhereEqualTo("isActive", true)
                .Limit(1)
                .GetSnapshotAsync();

            if (snapshot.Count > 0)
                throw BusinessException.Conflict("Ya existe un residente activo con ese número de casa.");
        }

        private async Task EnsurePlateIsFree(string plate)
        {
            var snapshot = await _context.Vehicles
                .WhereEqualTo("plate", plate)
                .WhereEqualTo("isActive", true)
                .Limit(1)
                .GetSnapshotAsync();

            if (snapshot.Count > 0)
                throw BusinessException.Conflict($"Ya existe un vehículo activo con la placa '{plate}'.");
        }

        private static string NormalizePlate(string plate) =>
            plate.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");

        // Contraseña temporal de 8 caracteres (sin caracteres ambiguos).
        private static string GenerateTempPassword(int length = 8)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            var bytes = RandomNumberGenerator.GetBytes(length);
            var sb = new StringBuilder(length);
            foreach (var b in bytes)
                sb.Append(chars[b % chars.Length]);
            return sb.ToString();
        }
    }
}
