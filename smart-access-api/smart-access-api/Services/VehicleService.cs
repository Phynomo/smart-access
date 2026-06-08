using Google.Cloud.Firestore;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Persistence;

namespace smart_access_api.Services
{
    public class VehicleService
    {
        private readonly FirestoreContext _context;

        public VehicleService(FirestoreContext context)
        {
            _context = context;
        }

        public async Task<Vehicle> Create(string residentId, VehicleCreateDto dto, string callerUserId, string callerRole)
        {
            await EnsureCanManageResident(residentId, callerUserId, callerRole);

            var plate = NormalizePlate(dto.Plate);
            await EnsurePlateIsFree(plate, excludeVehicleId: null);

            var vehicle = new Vehicle
            {
                Id = Guid.NewGuid().ToString(),
                ResidentId = residentId,
                Plate = plate,
                Brand = dto.Brand,
                Model = dto.Model,
                Color = dto.Color,
                Year = dto.Year,
                IsActive = true,
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
            };

            await _context.Vehicles.Document(vehicle.Id).SetAsync(vehicle);
            return vehicle;
        }

        public async Task<Vehicle> Update(string vehicleId, VehicleUpdateDto dto, string callerUserId, string callerRole)
        {
            var vehicle = await LoadOrThrow(vehicleId);
            await EnsureCanManageResident(vehicle.ResidentId, callerUserId, callerRole);

            var plate = NormalizePlate(dto.Plate);
            if (!string.Equals(plate, vehicle.Plate, StringComparison.OrdinalIgnoreCase))
                await EnsurePlateIsFree(plate, excludeVehicleId: vehicleId);

            vehicle.Plate = plate;
            vehicle.Brand = dto.Brand;
            vehicle.Model = dto.Model;
            vehicle.Color = dto.Color;
            vehicle.Year = dto.Year;

            await _context.Vehicles.Document(vehicleId).SetAsync(vehicle);
            return vehicle;
        }

        // Baja lógica del vehículo (no se borra, para no romper eventos históricos).
        public async Task Deactivate(string vehicleId, string callerUserId, string callerRole)
        {
            var vehicle = await LoadOrThrow(vehicleId);
            await EnsureCanManageResident(vehicle.ResidentId, callerUserId, callerRole);

            await _context.Vehicles.Document(vehicleId).UpdateAsync("isActive", false);
        }

        public async Task<List<Vehicle>> GetByResident(string residentId, bool onlyActive = true)
        {
            Query query = _context.Vehicles.WhereEqualTo("residentId", residentId);
            if (onlyActive)
                query = query.WhereEqualTo("isActive", true);

            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents
                .Select(d => d.ConvertTo<Vehicle>())
                .OrderBy(v => v.Plate)
                .ToList();
        }

        public async Task<Vehicle?> GetById(string id)
        {
            var doc = await _context.Vehicles.Document(id).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Vehicle>() : null;
        }

        // ----- Helpers -----

        private async Task<Vehicle> LoadOrThrow(string vehicleId)
        {
            var doc = await _context.Vehicles.Document(vehicleId).GetSnapshotAsync();
            if (!doc.Exists)
                throw BusinessException.NotFound("Vehículo no encontrado.");
            return doc.ConvertTo<Vehicle>();
        }

        // El admin puede gestionar cualquier vehículo. Un residente sólo los suyos.
        private async Task EnsureCanManageResident(string residentId, string callerUserId, string callerRole)
        {
            var residentDoc = await _context.Residents.Document(residentId).GetSnapshotAsync();
            if (!residentDoc.Exists)
                throw BusinessException.NotFound("Residente no encontrado.");

            var resident = residentDoc.ConvertTo<Resident>();
            if (!resident.IsActive)
                throw BusinessException.BadRequest("El residente está inactivo.");

            if (callerRole == UserRoles.Admin)
                return;

            if (resident.UserId != callerUserId)
                throw BusinessException.Forbidden("Sólo puedes gestionar tus propios vehículos.");
        }

        private async Task EnsurePlateIsFree(string plate, string? excludeVehicleId)
        {
            var snapshot = await _context.Vehicles
                .WhereEqualTo("plate", plate)
                .WhereEqualTo("isActive", true)
                .GetSnapshotAsync();

            var clash = snapshot.Documents.Any(d => d.Id != excludeVehicleId);
            if (clash)
                throw BusinessException.Conflict($"Ya existe un vehículo activo con la placa '{plate}'.");
        }

        private static string NormalizePlate(string plate) =>
            plate.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
    }
}
