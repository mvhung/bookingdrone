using EventMap.Api.Data;
using EventMap.Api.DTOs;
using EventMap.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ServicesController(AppDbContext context)
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

    [HttpGet]
    public async Task<ActionResult<List<ServiceTypeDto>>> GetServices([FromQuery] bool includeInactive = false)
    {
        var query = _context.ServiceTypes.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var list = await query
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .Select(s => new ServiceTypeDto(
                s.Id,
                s.Code,
                s.Name,
                s.Icon,
                s.Description,
                s.IsActive,
                s.DisplayOrder
            ))
            .ToListAsync();

        return Ok(list);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ServiceTypeDto>> CreateService([FromBody] SaveServiceTypeRequest request)
    {
        if (!await IsCallerAdminAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Mã dịch vụ và tên dịch vụ không được để trống" });
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.ServiceTypes.AnyAsync(s => s.Code == normalizedCode);
        if (exists)
        {
            return BadRequest(new { message = $"Mã dịch vụ '{normalizedCode}' đã tồn tại" });
        }

        var service = new ServiceType
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "🎯" : request.Icon.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };

        _context.ServiceTypes.Add(service);
        await _context.SaveChangesAsync();

        return Ok(new ServiceTypeDto(
            service.Id,
            service.Code,
            service.Name,
            service.Icon,
            service.Description,
            service.IsActive,
            service.DisplayOrder
        ));
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<ServiceTypeDto>> UpdateService(int id, [FromBody] SaveServiceTypeRequest request)
    {
        if (!await IsCallerAdminAsync())
        {
            return Forbid();
        }

        var service = await _context.ServiceTypes.FindAsync(id);
        if (service == null)
        {
            return NotFound(new { message = "Không tìm thấy loại dịch vụ" });
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (service.Code != normalizedCode)
        {
            var exists = await _context.ServiceTypes.AnyAsync(s => s.Code == normalizedCode && s.Id != id);
            if (exists)
            {
                return BadRequest(new { message = $"Mã dịch vụ '{normalizedCode}' đã tồn tại" });
            }
            service.Code = normalizedCode;
        }

        service.Name = request.Name.Trim();
        service.Icon = string.IsNullOrWhiteSpace(request.Icon) ? "🎯" : request.Icon.Trim();
        service.Description = request.Description?.Trim();
        service.IsActive = request.IsActive;
        service.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync();

        return Ok(new ServiceTypeDto(
            service.Id,
            service.Code,
            service.Name,
            service.Icon,
            service.Description,
            service.IsActive,
            service.DisplayOrder
        ));
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteService(int id)
    {
        if (!await IsCallerAdminAsync())
        {
            return Forbid();
        }

        var service = await _context.ServiceTypes.FindAsync(id);
        if (service == null)
        {
            return NotFound();
        }

        // Check if any jobs use this service code
        var inUse = await _context.EventJobs.AnyAsync(j => j.ServiceCode == service.Code);
        if (inUse)
        {
            // Instead of deleting and breaking existing jobs, softly deactivate
            service.IsActive = false;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Loại dịch vụ đã có job sử dụng nên được chuyển sang trạng thái ngưng hoạt động (inactive) để bảo toàn dữ liệu." });
        }

        _context.ServiceTypes.Remove(service);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
