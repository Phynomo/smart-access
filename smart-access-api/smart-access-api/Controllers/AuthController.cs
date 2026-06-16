using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.Common;
using smart_access_api.DTOs;
using smart_access_api.Models;
using smart_access_api.Services;

namespace smart_access_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.Login(dto.Identifier, dto.Password);
        return ApiResponse.Ok(result, "Inicio de sesión exitoso.").ToActionResult();
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = await _authService.Register(dto);
        return ApiResponse<UserResponseDto>
            .Created(UserResponseDto.From(user), "Cuenta creada con éxito.")
            .ToActionResult();
    }

    // El usuario solicita un enlace de recuperación. Siempre responde con éxito
    // (no revela si el correo/casa existe) para no permitir enumerar cuentas.
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        await _authService.ForgotPassword(dto.Identifier);
        return ApiResponse.Ok<object?>(null,
            "Si la cuenta existe, te enviamos un correo con instrucciones para recuperar tu contraseña.")
            .ToActionResult();
    }

    // El usuario llega desde el enlace del correo y define su nueva contraseña.
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        await _authService.ResetPassword(dto.Token, dto.NewPassword);
        return ApiResponse.Ok<object?>(null, "Contraseña actualizada. Ya puedes iniciar sesión.").ToActionResult();
    }

    // Cualquier usuario autenticado cambia su propia contraseña. En el primer cambio
    // (cuenta autogenerada) no se exige la contraseña anterior.
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw BusinessException.Unauthorized();

        var user = await _authService.ChangePassword(userId, dto);
        return ApiResponse.Ok(UserResponseDto.From(user), "Contraseña actualizada.").ToActionResult();
    }

    // Sólo el admin puede consultar usuarios por id o listar todos.
    [HttpGet("{id}")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _authService.GetById(id);
        if (user is null)
            throw BusinessException.NotFound("Usuario no encontrado.");

        return ApiResponse.Ok(UserResponseDto.From(user)).ToActionResult();
    }

    [HttpGet]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _authService.GetAll();
        var data = users.Select(UserResponseDto.From).ToList();
        return ApiResponse.Ok(data, "Listado de usuarios.").ToActionResult();
    }
}
