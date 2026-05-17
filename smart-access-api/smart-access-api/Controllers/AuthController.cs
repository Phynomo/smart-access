using Microsoft.AspNetCore.Mvc;
using smart_access_api.DTOs;
using smart_access_api.Services;

namespace smart_access_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        try
        {
            var user = await _authService.Login(dto.Email, dto.Password);
            return Ok(user);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = await _authService.Register(dto);
        return Ok(user);
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _authService.GetById(id);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _authService.GetAll();
        return Ok(users);
    }

}