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
