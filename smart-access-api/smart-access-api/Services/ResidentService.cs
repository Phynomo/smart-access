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

        public ResidentService(FirestoreContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private string QrSecret =>
            _configuration["Qr:Key"] ?? _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta la clave de firma de QR (Qr:Key o Jwt:Key).");

        // Crea, de forma atómica (WriteBatch): la cuenta User (rol resident), el
        // perfil Resident, el QR permanente del residente y los vehículos opcionales.
        public async Task<Resident> Create(ResidentCreateDto dto, string adminId)
        {
            await EnsureEmailIsFree(dto.Email);
            await EnsureHouseNumberIsFree(dto.HouseNumber);

            var userId = Guid.NewGuid().ToString();
            var residentId = Guid.NewGuid().ToString();
            var permanentQrId = Guid.NewGuid().ToString();
            var now = Timestamp.FromDateTime(DateTime.UtcNow);

            var user = new User
            {
                Id = userId,
                Name = dto.Name,
                Email = dto.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                HouseNumber = dto.HouseNumber.Trim(),
                Role = UserRoles.Resident,
                QrPermanentId = permanentQrId,
                IsActive = true,
                CreatedAt = now,
            };

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

            var batch = _context.Db.StartBatch();
            batch.Set(_context.Users.Document(userId), user);
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

        public async Task<Resident?> GetByUserId(string userId)
        {
            var snapshot = await _context.Residents
                .WhereEqualTo("userId", userId)
                .Limit(1)
                .GetSnapshotAsync();

            return snapshot.Count == 0 ? null : snapshot.Documents[0].ConvertTo<Resident>();
        }

        // ----- Validaciones de unicidad -----

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
    }
}
