using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Services;

namespace smart_access_api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = UserRoles.Admin)]
public class UsersController : ApiControllerBase
{
    private readonly AuthService _authService;

    public UsersController(AuthService authService)
    {
        _authService = authService;
    }

    // GET /api/users — listado completo con filtro opcional por rol.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? role)
    {
        var users = await _authService.GetAll();
        if (!string.IsNullOrWhiteSpace(role))
            users = users.Where(u => u.Role == role.ToLowerInvariant()).ToList();

        var data = users.Select(UserResponseDto.From).ToList();
        return ApiResponse.Ok(data, "Listado de usuarios.").ToActionResult();
    }

    // GET /api/users/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _authService.GetById(id);
        if (user is null) throw BusinessException.NotFound("Usuario no encontrado.");
        return ApiResponse.Ok(UserResponseDto.From(user)).ToActionResult();
    }

    // POST /api/users — crea un usuario con cualquier rol.
    // Si no se envía password, genera una temporal y la devuelve en la respuesta.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreateUserDto dto)
    {
        var (user, plainPassword) = await _authService.AdminCreate(dto, CurrentUserId);
        var data = new
        {
            User = UserResponseDto.From(user),
            TemporaryPassword = plainPassword, // null si el admin envió una contraseña
        };
        return ApiResponse<object>.Created(data, "Usuario creado correctamente.").ToActionResult();
    }

    // PUT /api/users/{id} — edita nombre, email, rol o número de casa.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] AdminUpdateUserDto dto)
    {
        var user = await _authService.AdminUpdate(id, dto);
        return ApiResponse.Ok(UserResponseDto.From(user), "Usuario actualizado.").ToActionResult();
    }

    // DELETE /api/users/{id} — desactiva sin borrar historial.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(string id)
    {
        if (id == CurrentUserId)
            throw BusinessException.BadRequest("No puedes desactivar tu propia cuenta.");

        var user = await _authService.AdminSetActive(id, false);
        return ApiResponse.Ok(UserResponseDto.From(user), "Usuario desactivado.").ToActionResult();
    }

    // POST /api/users/{id}/reactivate
    [HttpPost("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(string id)
    {
        var user = await _authService.AdminSetActive(id, true);
        return ApiResponse.Ok(UserResponseDto.From(user), "Usuario reactivado.").ToActionResult();
    }

    // POST /api/users/{id}/reset-password — genera y devuelve contraseña temporal.
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var (user, plain) = await _authService.AdminResetPassword(id);
        var data = new
        {
            User = UserResponseDto.From(user),
            TemporaryPassword = plain,
        };
        return ApiResponse.Ok(data, "Contraseña restablecida. Comparte la clave temporal con el usuario.").ToActionResult();
    }
}
