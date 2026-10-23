using EventMap.Api.Data;
using EventMap.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventMap.Api.Controllers;

public class SaveVenueRequest
{
    public string Name { get; set; } = string.Empty;
    public string? SubAddress { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _context;

    public VenuesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<SavedVenue>>> GetVenues()
    {
        var allJobs = await _context.EventJobs.ToListAsync();
        var existingVenues = await _context.SavedVenues.ToListAsync();

        // 2. Compute accurate actual job count for every venue
        foreach (var v in existingVenues)
        {
            var count = allJobs.Count(j => 
                (!string.IsNullOrWhiteSpace(j.LocationName) && j.LocationName.Trim().Equals(v.Name.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (Math.Abs(j.Latitude - v.Latitude) < 0.0008m && Math.Abs(j.Longitude - v.Longitude) < 0.0008m));
            v.UsageCount = count;
        }

        var sortedVenues = existingVenues
            .OrderByDescending(v => v.UsageCount)
            .ThenBy(v => v.Name)
            .ToList();

        return Ok(sortedVenues);
    }

    [HttpPost]
    public async Task<ActionResult<SavedVenue>> CreateVenue([FromBody] SaveVenueRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Tên địa điểm không được để trống" });

        var trimmedName = req.Name.Trim();
        var minLat = req.Latitude - 0.0008m;
        var maxLat = req.Latitude + 0.0008m;
        var minLng = req.Longitude - 0.0008m;
        var maxLng = req.Longitude + 0.0008m;

        var existing = await _context.SavedVenues.FirstOrDefaultAsync(v => 
            v.Name.ToLower() == trimmedName.ToLower() ||
            (v.Latitude >= minLat && v.Latitude <= maxLat && v.Longitude >= minLng && v.Longitude <= maxLng));

        var realCount = await _context.EventJobs.CountAsync(j =>
            j.LocationName.ToLower() == trimmedName.ToLower() ||
            (j.Latitude >= minLat && j.Latitude <= maxLat && j.Longitude >= minLng && j.Longitude <= maxLng));

        if (existing != null)
        {
            existing.Name = trimmedName;
            if (!string.IsNullOrWhiteSpace(req.SubAddress))
                existing.SubAddress = req.SubAddress.Trim();
            existing.UsageCount = realCount;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        var venue = new SavedVenue
        {
            Name = trimmedName,
            SubAddress = req.SubAddress?.Trim() ?? string.Empty,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            UsageCount = realCount,
            CreatedAt = DateTime.UtcNow
        };

        _context.SavedVenues.Add(venue);
        await _context.SaveChangesAsync();
        return Ok(venue);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SavedVenue>> UpdateVenue(int id, [FromBody] SaveVenueRequest req)
    {
        var venue = await _context.SavedVenues.FindAsync(id);
        if (venue == null) return NotFound();

        var oldName = venue.Name;
        var newName = !string.IsNullOrWhiteSpace(req.Name) ? req.Name.Trim() : venue.Name;
        venue.Name = newName;

        if (req.SubAddress != null)
            venue.SubAddress = req.SubAddress.Trim();
        if (req.Latitude != 0)
            venue.Latitude = req.Latitude;
        if (req.Longitude != 0)
            venue.Longitude = req.Longitude;

        // Synchronize all jobs at this venue's coordinates or previous name to the newly assigned reasonable name
        var vMinLat = venue.Latitude - 0.001m;
        var vMaxLat = venue.Latitude + 0.001m;
        var vMinLng = venue.Longitude - 0.001m;
        var vMaxLng = venue.Longitude + 0.001m;

        var jobsAtVenue = await _context.EventJobs
            .Where(j => j.LocationName == oldName || (j.Latitude >= vMinLat && j.Latitude <= vMaxLat && j.Longitude >= vMinLng && j.Longitude <= vMaxLng))
            .ToListAsync();

        foreach (var j in jobsAtVenue)
        {
            j.LocationName = newName;
            if (!string.IsNullOrWhiteSpace(venue.SubAddress))
            {
                j.SubAddress = venue.SubAddress;
            }
        }

        venue.UsageCount = jobsAtVenue.Count;
        await _context.SaveChangesAsync();
        return Ok(venue);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVenue(int id)
    {
        var venue = await _context.SavedVenues.FindAsync(id);
        if (venue == null) return NotFound();

        // Chỉ xóa địa điểm khỏi danh mục SavedVenues.
        // Giữ nguyên 100% tất cả các EventJobs đã lên lịch của thợ và khách hàng!
        _context.SavedVenues.Remove(venue);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
