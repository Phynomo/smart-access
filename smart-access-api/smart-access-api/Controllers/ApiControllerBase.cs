using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;

namespace smart_access_api.Controllers
{
    // Base para los controllers de la API. Centraliza la lectura del usuario
    // autenticado desde los claims del token JWT.
    [ApiController]
    [Route("api/[controller]")]
    public abstract class ApiControllerBase : ControllerBase
    {
        // Id del usuario autenticado (claim NameIdentifier). Si falta, el token es
        // inválido → 401.
        protected string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw BusinessException.Unauthorized();

        protected string CurrentUserRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }
}
