using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Services;

namespace smart_access_api.Controllers
{
    [Authorize]
    public class ResidentsController : ApiControllerBase
    {
        private readonly ResidentService _residentService;

        public ResidentsController(ResidentService residentService)
        {
            _residentService = residentService;
        }

        // Admin: crea residente (+ cuenta de login + QR permanente + vehículos).
        [HttpPost]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Create([FromBody] ResidentCreateDto dto)
        {
            var resident = await _residentService.Create(dto, CurrentUserId);
            var body = ApiResponse<ResidentResponseDto>.Created(
                ResidentResponseDto.From(resident), "Residente creado con éxito.");
            return body.ToActionResult();
        }

        // Admin: edita la información del residente.
        [HttpPut("{id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Update(string id, [FromBody] ResidentUpdateDto dto)
        {
            var resident = await _residentService.Update(id, dto);
            return ApiResponse.Ok(ResidentResponseDto.From(resident), "Residente actualizado.").ToActionResult();
        }

        // Admin: desactiva (no elimina) al residente.
        [HttpDelete("{id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Deactivate(string id)
        {
            await _residentService.Deactivate(id);
            return ApiResponse.Ok<object?>(null, "Residente desactivado.").ToActionResult();
        }

        // Admin: reactiva un residente dado de baja (vuelve isActive = true).
        [HttpPost("{id}/reactivate")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Reactivate(string id)
        {
            await _residentService.Reactivate(id);
            return ApiResponse.Ok<object?>(null, "Residente reactivado.").ToActionResult();
        }

        // Admin: restablece la contraseña del residente. Genera una temporal y se la
        // envía por correo; el residente deberá cambiarla en su próximo ingreso.
        [HttpPost("{id}/reset-password")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> ResetPassword(string id)
        {
            await _residentService.ResetPassword(id);
            return ApiResponse.Ok<object?>(
                null, "Se envió un correo al residente para restablecer su contraseña.").ToActionResult();
        }

        // Seguridad: busca un residente por número de casa para el registro manual.
        // Devuelve sólo id/nombre/casa — sin datos sensibles.
        [HttpGet("lookup")]
        [Authorize(Roles = $"{UserRoles.Security},{UserRoles.Admin}")]
        public async Task<IActionResult> Lookup([FromQuery] string houseNumber)
        {
            if (string.IsNullOrWhiteSpace(houseNumber))
                throw BusinessException.BadRequest("El número de casa es obligatorio.");

            var resident = await _residentService.GetByHouseNumber(houseNumber);
            if (resident is null)
                throw BusinessException.NotFound("No se encontró un residente activo con ese número de casa.");

            return ApiResponse.Ok(ResidentLookupDto.From(resident)).ToActionResult();
        }

        // Admin: lista todos los residentes (filtro opcional onlyActive).
        [HttpGet]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GetAll([FromQuery] bool? onlyActive)
        {
            var residents = await _residentService.GetAll(onlyActive);
            var data = residents.Select(ResidentResponseDto.From).ToList();
            return ApiResponse.Ok(data, "Listado de residentes.").ToActionResult();
        }

        // Admin: detalle de un residente.
        [HttpGet("{id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GetById(string id)
        {
            var resident = await _residentService.GetById(id);
            if (resident is null)
                throw BusinessException.NotFound("Residente no encontrado.");

            return ApiResponse.Ok(ResidentResponseDto.From(resident)).ToActionResult();
        }

        // Residente: su propio perfil.
        [HttpGet("me")]
        [Authorize(Roles = UserRoles.Resident)]
        public async Task<IActionResult> GetMine()
        {
            var resident = await _residentService.GetByUserId(CurrentUserId);
            if (resident is null)
                throw BusinessException.NotFound("No tienes un perfil de residente asociado.");

            return ApiResponse.Ok(ResidentResponseDto.From(resident)).ToActionResult();
        }
    }
}
