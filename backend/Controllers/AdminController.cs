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
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    private async Task<bool> IsCallerAdminAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return false;
        var user = await _context.Users.FindAsync(userId);
        return user?.IsAdmin == true;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var userId) ? userId : 0;
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserDto>>> GetUsers()
    {
        if (!await IsCallerAdminAsync())
        {
            return Forbid();
        }

        var users = await _context.Users
            .OrderBy(u => u.Id)
            .ToListAsync();

        var dtos = users.Select(u => new AdminUserDto(
            u.Id,
            u.Name,
            u.AvatarUrl,
            JsonSerializer.Deserialize<List<string>>(u.ServicesJson) ?? new List<string>(),
            u.IsAdmin,
            u.OfficeBusyEnabled,
            u.OfficeBusyStart.ToString("HH:mm"),
            u.OfficeBusyEnd.ToString("HH:mm")
        )).ToList();

        return Ok(dtos);
    }

    [HttpPost("users")]
    public async Task<ActionResult<AdminUserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!await IsCallerAdminAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Tên thành viên không được để trống" });
        }

        var pin = string.IsNullOrWhiteSpace(request.Pin) ? "1234" : request.Pin.Trim();
        var pinHash = BCrypt.Net.BCrypt.HashPassword(pin);

        var avatar = string.IsNullOrWhiteSpace(request.AvatarUrl)
            ? $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(request.Name.Trim())}"
            : request.AvatarUrl.Trim();

        var user = new User
        {
            Name = request.Name.Trim(),
            PinHash = pinHash,
            AvatarUrl = avatar,
            ServicesJson = JsonSerializer.Serialize(request.Services ?? new List<string>()),
            IsAdmin = request.IsAdmin,
            OfficeBusyEnabled = false,
            OfficeBusyStart = new TimeOnly(8, 0),
            OfficeBusyEnd = new TimeOnly(17, 30)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new AdminUserDto(
            user.Id,
            user.Name,
            user.AvatarUrl,
            request.Services ?? new List<string>(),
            user.IsAdmin,
            user.OfficeBusyEnabled,
            user.OfficeBusyStart.ToString("HH:mm"),
            user.OfficeBusyEnd.ToString("HH:mm")
        ));
    }

    [HttpPut("users/{id}")]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(int id, [FromBody] UpdateUserAdminRequest request)
    {
        if (!await IsCallerAdminAsync())
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy thành viên" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Tên không được để trống" });
        }

        user.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            user.AvatarUrl = request.AvatarUrl.Trim();
        }
        user.ServicesJson = JsonSerializer.Serialize(request.Services ?? new List<string>());
        user.IsAdmin = request.IsAdmin;
        user.OfficeBusyEnabled = request.OfficeBusyEnabled;

        if (TimeOnly.TryParse(request.OfficeBusyStart, out var start))
            user.OfficeBusyStart = start;

        if (TimeOnly.TryParse(request.OfficeBusyEnd, out var end))
            user.OfficeBusyEnd = end;

        await _context.SaveChangesAsync();

        return Ok(new AdminUserDto(
            user.Id,
            user.Name,
            user.AvatarUrl,
            request.Services ?? new List<string>(),
            user.IsAdmin,
            user.OfficeBusyEnabled,
            user.OfficeBusyStart.ToString("HH:mm"),
            user.OfficeBusyEnd.ToString("HH:mm")
        ));
    }

    [HttpPut("users/{id}/reset-pin")]
    public async Task<IActionResult> ResetPin(int id, [FromBody] ResetPinRequest request)
    {
        if (!await IsCallerAdminAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.NewPin) || request.NewPin.Length < 4)
        {
            return BadRequest(new { message = "Mã PIN mới phải có ít nhất 4 ký tự" });
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng" });
        }

        user.PinHash = BCrypt.Net.BCrypt.HashPassword(request.NewPin.Trim());
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Đã đặt lại mã PIN thành công cho {user.Name}" });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!await IsCallerAdminAsync())
        {
            return Forbid();
        }

        var currentUserId = GetCurrentUserId();
        if (id == currentUserId)
        {
            return BadRequest(new { message = "Không thể tự xóa tài khoản của chính mình" });
        }

        var user = await _context.Users
            .Include(u => u.JobsCreated)
            .Include(u => u.JobsOwned)
            .Include(u => u.Assignments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        if (user.JobsCreated.Any() || user.JobsOwned.Any() || user.Assignments.Any())
        {
            return BadRequest(new { 
                message = $"Không thể xóa thành viên '{user.Name}' vì đã có dữ liệu lịch làm việc hoặc công việc liên quan. Bạn có thể xóa quyền Admin hoặc đổi tên để lưu trữ." 
            });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
