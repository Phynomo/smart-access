using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Services;

namespace smart_access_api.Controllers
{
    [Authorize]
    public class VehiclesController : ApiControllerBase
    {
        private readonly VehicleService _vehicleService;
        private readonly ResidentService _residentService;

        public VehiclesController(VehicleService vehicleService, ResidentService residentService)
        {
            _vehicleService = vehicleService;
            _residentService = residentService;
        }

        // Admin: registra un vehículo para un residente dado.
        [HttpPost("resident/{residentId}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> CreateForResident(string residentId, [FromBody] VehicleCreateDto dto)
        {
            var vehicle = await _vehicleService.Create(residentId, dto, CurrentUserId, CurrentUserRole);
            return ApiResponse<VehicleResponseDto>
                .Created(VehicleResponseDto.From(vehicle), "Vehículo registrado.")
                .ToActionResult();
        }

        // Admin: lista los vehículos de un residente.
        [HttpGet("resident/{residentId}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GetByResident(string residentId, [FromQuery] bool onlyActive = true)
        {
            var vehicles = await _vehicleService.GetByResident(residentId, onlyActive);
            var data = vehicles.Select(VehicleResponseDto.From).ToList();
            return ApiResponse.Ok(data).ToActionResult();
        }

        // Residente: registra uno de sus propios vehículos.
        [HttpPost("mine")]
        [Authorize(Roles = UserRoles.Resident)]
        public async Task<IActionResult> CreateMine([FromBody] VehicleCreateDto dto)
        {
            var residentId = await ResolveMyResidentId();
            var vehicle = await _vehicleService.Create(residentId, dto, CurrentUserId, CurrentUserRole);
            return ApiResponse<VehicleResponseDto>
                .Created(VehicleResponseDto.From(vehicle), "Vehículo registrado.")
                .ToActionResult();
        }

        // Residente: lista sus propios vehículos.
        [HttpGet("mine")]
        [Authorize(Roles = UserRoles.Resident)]
        public async Task<IActionResult> GetMine([FromQuery] bool onlyActive = true)
        {
            var residentId = await ResolveMyResidentId();
            var vehicles = await _vehicleService.GetByResident(residentId, onlyActive);
            var data = vehicles.Select(VehicleResponseDto.From).ToList();
            return ApiResponse.Ok(data).ToActionResult();
        }

        // Admin o dueño: edita un vehículo (la pertenencia se valida en el servicio).
        [HttpPut("{id}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Resident)]
        public async Task<IActionResult> Update(string id, [FromBody] VehicleUpdateDto dto)
        {
            var vehicle = await _vehicleService.Update(id, dto, CurrentUserId, CurrentUserRole);
            return ApiResponse.Ok(VehicleResponseDto.From(vehicle), "Vehículo actualizado.").ToActionResult();
        }

        // Admin o dueño: da de baja un vehículo (no lo elimina).
        [HttpDelete("{id}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Resident)]
        public async Task<IActionResult> Deactivate(string id)
        {
            await _vehicleService.Deactivate(id, CurrentUserId, CurrentUserRole);
            return ApiResponse.Ok<object?>(null, "Vehículo dado de baja.").ToActionResult();
        }

        private async Task<string> ResolveMyResidentId()
        {
            var resident = await _residentService.GetByUserId(CurrentUserId);
            if (resident is null)
                throw BusinessException.NotFound("No tienes un perfil de residente asociado.");
            return resident.Id;
        }
    }
}
