using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Services;

namespace smart_access_api.Controllers
{
    [Authorize]
    public class AccessController : ApiControllerBase
    {
        private readonly AccessService _accessService;
        private readonly ResidentService _residentService;

        public AccessController(AccessService accessService, ResidentService residentService)
        {
            _accessService = accessService;
            _residentService = residentService;
        }

        // Seguridad: valida un QR escaneado y registra el evento (autorizado o rechazado).
        [HttpPost("validate")]
        [Authorize(Roles = UserRoles.Security)]
        public async Task<IActionResult> Validate([FromBody] ValidateQrDto dto)
        {
            var result = await _accessService.ValidateQr(dto, CurrentUserId);
            var message = result.Authorized ? "Acceso autorizado." : $"Acceso rechazado: {result.RejectionReason}";
            // Siempre 200: la validación se ejecutó correctamente; el resultado
            // (autorizado/rechazado) va dentro del cuerpo.
            return ApiResponse.Ok(result, message).ToActionResult();
        }

        // Seguridad: registra un ingreso manual (residente conocido o visitante).
        [HttpPost("manual")]
        [Authorize(Roles = UserRoles.Security)]
        public async Task<IActionResult> Manual([FromBody] ManualEntryDto dto)
        {
            var ev = await _accessService.RegisterManual(dto, CurrentUserId);
            return ApiResponse<AccessEventResponseDto>
                .Created(AccessEventResponseDto.From(ev), "Ingreso manual registrado.")
                .ToActionResult();
        }

        // Admin: log completo de accesos, con filtros opcionales.
        [HttpGet]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GetAll([FromQuery] AccessQueryDto filters)
        {
            var events = await _accessService.Query(filters);
            var data = events.Select(AccessEventResponseDto.From).ToList();
            return ApiResponse.Ok(data, "Log de accesos.").ToActionResult();
        }

        // Residente: su propio historial de accesos.
        [HttpGet("mine")]
        [Authorize(Roles = UserRoles.Resident)]
        public async Task<IActionResult> GetMine()
        {
            var resident = await _residentService.GetByUserId(CurrentUserId);
            if (resident is null)
                throw BusinessException.NotFound("No tienes un perfil de residente asociado.");

            var events = await _accessService.GetByResident(resident.Id);
            var data = events.Select(AccessEventResponseDto.From).ToList();
            return ApiResponse.Ok(data).ToActionResult();
        }

        // Seguridad: historial del turno actual del guardia.
        [HttpGet("shift")]
        [Authorize(Roles = UserRoles.Security)]
        public async Task<IActionResult> GetShift([FromQuery] DateTime? since)
        {
            var events = await _accessService.GetShiftLog(CurrentUserId, since);
            var data = events.Select(AccessEventResponseDto.From).ToList();
            return ApiResponse.Ok(data, "Historial del turno.").ToActionResult();
        }
    }
}
