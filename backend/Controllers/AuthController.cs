using EventMap.Api.Data;
using EventMap.Api.DTOs;
using EventMap.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace EventMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtService _jwtService;

    public AuthController(AppDbContext context, JwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<UserPickerDto>>> GetUsersForPicker()
    {
        var users = await _context.Users
            .OrderBy(u => u.Id)
            .Select(u => new UserPickerDto(
                u.Id,
                u.Name,
                u.AvatarUrl,
                JsonSerializer.Deserialize<List<string>>(u.ServicesJson, (JsonSerializerOptions?)null) ?? new List<string>()
            ))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.BusyDates)
            .FirstOrDefaultAsync(u => u.Id == request.UserId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Verify PIN (or fallback "1234" in dev)
        bool isValid = false;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(request.Pin, user.PinHash);
        }
        catch
        {
            isValid = request.Pin == "1234";
        }

        if (!isValid && request.Pin == "1234")
        {
            isValid = true;
        }

        if (!isValid)
        {
            return BadRequest(new { message = "Mã PIN không đúng. Mặc định là 1234." });
        }

        var token = _jwtService.GenerateToken(user);
        var services = JsonSerializer.Deserialize<List<string>>(user.ServicesJson) ?? new List<string>();

        var userProfile = new UserProfileDto(
            user.Id,
            user.Name,
            user.AvatarUrl,
            services,
            user.OfficeBusyEnabled,
            user.OfficeBusyStart.ToString("HH:mm"),
            user.OfficeBusyEnd.ToString("HH:mm"),
            user.BusyDates.Select(b => new BusyDateItemDto(b.Id, b.BusyDate.ToString("yyyy-MM-dd"), b.Reason)).ToList()
        );

        return Ok(new LoginResponse(token, userProfile));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUser()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized();
        }

        var user = await _context.Users
            .Include(u => u.BusyDates)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (user == null)
        {
            return NotFound();
        }

        var services = JsonSerializer.Deserialize<List<string>>(user.ServicesJson) ?? new List<string>();

        return Ok(new UserProfileDto(
            user.Id,
            user.Name,
            user.AvatarUrl,
            services,
            user.OfficeBusyEnabled,
            user.OfficeBusyStart.ToString("HH:mm"),
            user.OfficeBusyEnd.ToString("HH:mm"),
            user.BusyDates.Select(b => new BusyDateItemDto(b.Id, b.BusyDate.ToString("yyyy-MM-dd"), b.Reason)).ToList()
        ));
    }
}
