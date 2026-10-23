using EventMap.Api.Data;
using EventMap.Api.DTOs;
using EventMap.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace EventMap.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProfileController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var user = await _context.Users
            .Include(u => u.BusyDates)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();

        var services = JsonSerializer.Deserialize<List<string>>(user.ServicesJson) ?? new List<string>();

        return Ok(new UserProfileDto(
            user.Id,
            user.Name,
            user.AvatarUrl,
            services,
            user.IsAdmin,
            user.OfficeBusyEnabled,
            user.OfficeBusyStart.ToString("HH:mm"),
            user.OfficeBusyEnd.ToString("HH:mm"),
            user.BusyDates.Select(b => new BusyDateItemDto(
                b.Id,
                b.BusyDate.ToString("yyyy-MM-dd"),
                b.Reason,
                b.StartTime?.ToString("HH:mm"),
                b.EndTime?.ToString("HH:mm")
            )).ToList()
        ));
    }

    [HttpPut]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var user = await _context.Users
            .Include(u => u.BusyDates)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();

        if (TimeOnly.TryParse(request.OfficeBusyStart, out var start))
            user.OfficeBusyStart = start;

        if (TimeOnly.TryParse(request.OfficeBusyEnd, out var end))
            user.OfficeBusyEnd = end;

        user.OfficeBusyEnabled = request.OfficeBusyEnabled;
        user.ServicesJson = JsonSerializer.Serialize(request.Services ?? new List<string>());

        await _context.SaveChangesAsync();

        var services = JsonSerializer.Deserialize<List<string>>(user.ServicesJson) ?? new List<string>();

        return Ok(new UserProfileDto(
            user.Id,
            user.Name,
            user.AvatarUrl,
            services,
            user.IsAdmin,
            user.OfficeBusyEnabled,
            user.OfficeBusyStart.ToString("HH:mm"),
            user.OfficeBusyEnd.ToString("HH:mm"),
            user.BusyDates.Select(b => new BusyDateItemDto(
                b.Id,
                b.BusyDate.ToString("yyyy-MM-dd"),
                b.Reason,
                b.StartTime?.ToString("HH:mm"),
                b.EndTime?.ToString("HH:mm")
            )).ToList()
        ));
    }

    [HttpPost("busy-dates")]
    public async Task<ActionResult<BusyDateItemDto>> AddBusyDate([FromBody] AddBusyDateRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        if (!DateOnly.TryParse(request.Date, out var date))
            return BadRequest(new { message = "Invalid date format" });

        TimeOnly? startTime = null;
        if (!string.IsNullOrWhiteSpace(request.StartTime) && TimeOnly.TryParse(request.StartTime, out var s))
        {
            startTime = s;
        }

        TimeOnly? endTime = null;
        if (!string.IsNullOrWhiteSpace(request.EndTime) && TimeOnly.TryParse(request.EndTime, out var e))
        {
            endTime = e;
        }

        var busyDate = new UserBusyDate
        {
            UserId = userId,
            BusyDate = date,
            StartTime = startTime,
            EndTime = endTime,
            Reason = request.Reason ?? "Báo nghỉ"
        };

        _context.UserBusyDates.Add(busyDate);
        await _context.SaveChangesAsync();

        return Ok(new BusyDateItemDto(
            busyDate.Id,
            busyDate.BusyDate.ToString("yyyy-MM-dd"),
            busyDate.Reason,
            busyDate.StartTime?.ToString("HH:mm"),
            busyDate.EndTime?.ToString("HH:mm")
        ));
    }

    [HttpDelete("busy-dates/{id}")]
    public async Task<IActionResult> RemoveBusyDate(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var busyDate = await _context.UserBusyDates.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
        if (busyDate == null) return NotFound();

        _context.UserBusyDates.Remove(busyDate);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
