using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Services;

namespace smart_access_api.Controllers
{
    [Authorize]
    public class QRCodesController : ApiControllerBase
    {
        private readonly QRService _qrService;
        private readonly ResidentService _residentService;

        public QRCodesController(QRService qrService, ResidentService residentService)
        {
            _qrService = qrService;
            _residentService = residentService;
        }

        // Residente: su QR permanente.
        [HttpGet("mine/permanent")]
        [Authorize(Roles = UserRoles.Resident)]
        public async Task<IActionResult> GetMyPermanent()
        {
            var residentId = await ResolveMyResidentId();
            var qr = await _qrService.GetPermanentForResident(residentId);
            if (qr is null)
                throw BusinessException.NotFound("No tienes un QR permanente asignado.");

            return ApiResponse.Ok(QrCodeResponseDto.From(qr)).ToActionResult();
        }

        // Residente: todos sus QR (permanente + de visita).
        [HttpGet("mine")]
        [Authorize(Roles = UserRoles.Resident)]
        public async Task<IActionResult> GetMine()
        {
            var residentId = await ResolveMyResidentId();
            var qrs = await _qrService.GetByResident(residentId);
            var data = qrs.Select(QrCodeResponseDto.From).ToList();
            return ApiResponse.Ok(data).ToActionResult();
        }

        // Residente: genera un QR de visita (date o long_term) para sí mismo.
        [HttpPost("mine")]
        [Authorize(Roles = UserRoles.Resident)]
        public async Task<IActionResult> GenerateMine([FromBody] GenerateQrDto dto)
        {
            var residentId = await ResolveMyResidentId();
            var qr = await _qrService.GenerateVisitQr(residentId, dto, CurrentUserId, CurrentUserRole);
            return ApiResponse<QrCodeResponseDto>
                .Created(QrCodeResponseDto.From(qr), "QR generado con éxito.")
                .ToActionResult();
        }

        // Admin: genera un QR de visita en nombre de un residente.
        [HttpPost("resident/{residentId}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GenerateForResident(string residentId, [FromBody] GenerateQrDto dto)
        {
            var qr = await _qrService.GenerateVisitQr(residentId, dto, CurrentUserId, CurrentUserRole);
            return ApiResponse<QrCodeResponseDto>
                .Created(QrCodeResponseDto.From(qr), "QR generado con éxito.")
                .ToActionResult();
        }

        // Admin: lista los QR de un residente.
        [HttpGet("resident/{residentId}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GetByResident(string residentId)
        {
            var qrs = await _qrService.GetByResident(residentId);
            var data = qrs.Select(QrCodeResponseDto.From).ToList();
            return ApiResponse.Ok(data).ToActionResult();
        }

        // Admin o dueño: revoca un QR de visita (la pertenencia se valida en el servicio).
        [HttpDelete("{id}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Resident)]
        public async Task<IActionResult> Revoke(string id)
        {
            await _qrService.Revoke(id, CurrentUserId, CurrentUserRole);
            return ApiResponse.Ok<object?>(null, "QR revocado.").ToActionResult();
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
