using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Services;

namespace smart_access_api.Controllers
{
    [Authorize(Roles = UserRoles.Admin)]
    public class ReportsController : ApiControllerBase
    {
        private readonly ReportService _reportService;

        public ReportsController(ReportService reportService)
        {
            _reportService = reportService;
        }

        // Admin: agregados para el dashboard (tarjetas, gráficos y tendencia).
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics([FromQuery] ReportQueryDto query)
        {
            var stats = await _reportService.GetStatistics(query);
            return ApiResponse.Ok(stats, "Estadísticas de acceso.").ToActionResult();
        }
    }
}
