using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Services;

namespace smart_access_api.Controllers
{
    // Notificaciones del usuario autenticado (residente). El frontend las consulta
    // por REST con su JWT, en vez de leer Firestore directamente desde el cliente.
    [Authorize]
    public class NotificationsController : ApiControllerBase
    {
        private readonly NotificationService _notifications;

        public NotificationsController(NotificationService notifications)
        {
            _notifications = notifications;
        }

        // GET /api/notifications — las más recientes del usuario autenticado.
        [HttpGet]
        public async Task<IActionResult> GetMine()
        {
            var items = await _notifications.GetForUserAsync(CurrentUserId);
            var data = items.Select(NotificationResponseDto.From).ToList();
            return ApiResponse.Ok(data, "Notificaciones del usuario.").ToActionResult();
        }

        // POST /api/notifications/{id}/read — marca una como leída.
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkRead(string id)
        {
            await _notifications.MarkAsReadAsync(id, CurrentUserId);
            return ApiResponse.Ok<object?>(null, "Notificación marcada como leída.").ToActionResult();
        }

        // POST /api/notifications/read-all — marca todas las no leídas como leídas.
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var count = await _notifications.MarkAllReadAsync(CurrentUserId);
            return ApiResponse.Ok(new { updated = count }, "Notificaciones marcadas como leídas.").ToActionResult();
        }
    }
}
