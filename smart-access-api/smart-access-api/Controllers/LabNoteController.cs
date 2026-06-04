using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using smart_access_api.DTOs;
using smart_access_api.Services;

namespace smart_access_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LabNoteController : ControllerBase
{
    private readonly LabNoteService _labNoteService;

    public LabNoteController(LabNoteService labNoteService)
    {
        _labNoteService = labNoteService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LabNoteDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var note = await _labNoteService.Create(dto, userId);
            return CreatedAtAction(nameof(GetMine), new { id = note.Id }, note);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var notes = await _labNoteService.GetByUser(userId);
        return Ok(notes);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _labNoteService.Delete(id, userId);
        return result switch
        {
            DeleteResult.NotFound => NotFound(),
            DeleteResult.Forbidden => Forbid(),
            _ => NoContent()
        };
    }
}
