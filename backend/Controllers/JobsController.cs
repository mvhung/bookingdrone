using EventMap.Api.Data;
using EventMap.Api.DTOs;
using EventMap.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventMap.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _context;

    public JobsController(AppDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : 0;
    }

    // GET /api/jobs?month=2026-10&date=2026-10-18
    [HttpGet]
    public async Task<ActionResult<List<JobResponseDto>>> GetJobs([FromQuery] string? month, [FromQuery] string? date)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0) return Unauthorized();

        var query = _context.EventJobs
            .Include(j => j.CreatedByUser)
            .Include(j => j.OwnerUser)
            .Include(j => j.Assignments)
                .ThenInclude(a => a.User)
            .AsNoTracking();

        // RULE A: Strict Job & Map Visibility
        query = query.Where(j => j.OwnerUserId == currentUserId || j.Assignments.Any(a => a.UserId == currentUserId));

        // Date / Month filter
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedDate))
        {
            query = query.Where(j => j.EventDate == parsedDate);
        }
        else if (!string.IsNullOrWhiteSpace(month))
        {
            // format: YYYY-MM
            var parts = month.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m))
            {
                query = query.Where(j => j.EventDate.Year == y && j.EventDate.Month == m);
            }
        }

        var jobs = await query
            .OrderBy(j => j.EventDate)
            .ThenBy(j => j.StartTime)
            .ToListAsync();

        var result = jobs.Select(j => MapToDto(j, currentUserId)).ToList();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobResponseDto>> GetJobById(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0) return Unauthorized();

        var job = await _context.EventJobs
            .Include(j => j.CreatedByUser)
            .Include(j => j.OwnerUser)
            .Include(j => j.Assignments)
                .ThenInclude(a => a.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null) return NotFound();

        // Enforce visibility
        bool hasAccess = job.OwnerUserId == currentUserId || job.Assignments.Any(a => a.UserId == currentUserId);
        if (!hasAccess) return Forbid();

        return Ok(MapToDto(job, currentUserId));
    }

    [HttpPost]
    public async Task<ActionResult<JobResponseDto>> CreateJob([FromBody] SaveJobRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0) return Unauthorized();

        if (!DateOnly.TryParse(request.EventDate, out var eventDate))
            return BadRequest(new { message = "Invalid EventDate format (yyyy-MM-dd)" });

        if (!TimeOnly.TryParse(request.StartTime, out var startTime) || !TimeOnly.TryParse(request.EndTime, out var endTime))
            return BadRequest(new { message = "Invalid Time format (HH:mm)" });

        var job = new EventJob
        {
            CreatedByUserId = currentUserId,
            OwnerUserId = request.OwnerUserId,
            ExternalClientName = request.ExternalClientName,
            EventDate = eventDate,
            StartTime = startTime,
            EndTime = endTime,
            ServiceCode = request.ServiceCode,
            LocationName = request.LocationName,
            SubAddress = request.SubAddress ?? string.Empty,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            // If current user is not the Owner, only allow saving ClientTotalRevenue if specified and valid
            ClientTotalRevenue = request.ClientTotalRevenue,
            ExternalWorkersNote = request.ExternalWorkersNote ?? string.Empty,
            Note = request.Note ?? string.Empty,
            Assignments = new List<JobAssignment>()
        };

        if (request.Assignments != null)
        {
            foreach (var a in request.Assignments)
            {
                job.Assignments.Add(new JobAssignment
                {
                    UserId = a.UserId,
                    CastAmount = a.CastAmount,
                    IsPaid = a.IsPaid,
                    PersonalNote = a.PersonalNote ?? string.Empty
                });
            }
        }

        // If no crew selected, assign creator by default
        if (job.Assignments.Count == 0)
        {
            job.Assignments.Add(new JobAssignment
            {
                UserId = currentUserId,
                CastAmount = 2000000m,
                IsPaid = false,
                PersonalNote = ""
            });
        }

        _context.EventJobs.Add(job);
        await _context.SaveChangesAsync();
        await SyncSavedVenueAsync(job.LocationName, job.SubAddress, job.Latitude, job.Longitude);

        // Reload with navigations
        var created = await _context.EventJobs
            .Include(j => j.CreatedByUser)
            .Include(j => j.OwnerUser)
            .Include(j => j.Assignments)
                .ThenInclude(a => a.User)
            .FirstAsync(j => j.Id == job.Id);

        return CreatedAtAction(nameof(GetJobById), new { id = created.Id }, MapToDto(created, currentUserId));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<JobResponseDto>> UpdateJob(int id, [FromBody] SaveJobRequest request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0) return Unauthorized();

        var job = await _context.EventJobs
            .Include(j => j.Assignments)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null) return NotFound();

        bool isOwner = job.OwnerUserId == currentUserId;
        bool isCreator = job.CreatedByUserId == currentUserId;

        // RULE: Only the person who created the job or is the job owner can edit!
        if (!isOwner && !isCreator)
        {
            return Forbid();
        }

        if (!DateOnly.TryParse(request.EventDate, out var eventDate))
            return BadRequest(new { message = "Invalid EventDate format (yyyy-MM-dd)" });

        if (!TimeOnly.TryParse(request.StartTime, out var startTime) || !TimeOnly.TryParse(request.EndTime, out var endTime))
            return BadRequest(new { message = "Invalid Time format (HH:mm)" });

        job.EventDate = eventDate;
        job.StartTime = startTime;
        job.EndTime = endTime;
        job.ServiceCode = request.ServiceCode;
        job.LocationName = request.LocationName;
        job.SubAddress = request.SubAddress ?? string.Empty;
        job.Latitude = request.Latitude;
        job.Longitude = request.Longitude;
        job.OwnerUserId = request.OwnerUserId;
        job.ExternalClientName = request.ExternalClientName;
        job.ExternalWorkersNote = request.ExternalWorkersNote ?? string.Empty;
        job.Note = request.Note ?? string.Empty;
        job.ClientTotalRevenue = request.ClientTotalRevenue;

        // Sync assignments
        var existingAssignments = job.Assignments.ToList();
        var newAssignmentsReq = request.Assignments ?? new List<JobAssignmentItemDto>();

        // Remove assignments no longer present
        foreach (var ea in existingAssignments)
        {
            if (!newAssignmentsReq.Any(na => na.UserId == ea.UserId))
            {
                _context.JobAssignments.Remove(ea);
            }
        }

        // Update or add
        foreach (var na in newAssignmentsReq)
        {
            var ea = existingAssignments.FirstOrDefault(x => x.UserId == na.UserId);
            if (ea != null)
            {
                ea.CastAmount = na.CastAmount;
                ea.IsPaid = na.IsPaid;
                ea.PersonalNote = na.PersonalNote ?? string.Empty;
            }
            else
            {
                job.Assignments.Add(new JobAssignment
                {
                    JobId = job.Id,
                    UserId = na.UserId,
                    CastAmount = na.CastAmount,
                    IsPaid = na.IsPaid,
                    PersonalNote = na.PersonalNote ?? string.Empty
                });
            }
        }

        await _context.SaveChangesAsync();
        await SyncSavedVenueAsync(job.LocationName, job.SubAddress, job.Latitude, job.Longitude);

        var updated = await _context.EventJobs
            .Include(j => j.CreatedByUser)
            .Include(j => j.OwnerUser)
            .Include(j => j.Assignments)
                .ThenInclude(a => a.User)
            .FirstAsync(j => j.Id == job.Id);

        return Ok(MapToDto(updated, currentUserId));
    }

    private async Task SyncSavedVenueAsync(string? locationName, string? subAddress, decimal lat, decimal lng)
    {
        if (string.IsNullOrWhiteSpace(locationName)) return;
        var trimmed = locationName.Trim();
        var minLat = lat - 0.0008m;
        var maxLat = lat + 0.0008m;
        var minLng = lng - 0.0008m;
        var maxLng = lng + 0.0008m;

        var existing = await _context.SavedVenues.FirstOrDefaultAsync(v => 
            v.Name.ToLower() == trimmed.ToLower() ||
            (v.Latitude >= minLat && v.Latitude <= maxLat && v.Longitude >= minLng && v.Longitude <= maxLng));

        if (existing != null)
        {
            if (string.IsNullOrWhiteSpace(existing.SubAddress) && !string.IsNullOrWhiteSpace(subAddress))
            {
                existing.SubAddress = subAddress.Trim();
            }
        }
        else
        {
            existing = new SavedVenue
            {
                Name = trimmed,
                SubAddress = subAddress?.Trim() ?? string.Empty,
                Latitude = lat,
                Longitude = lng,
                UsageCount = 1,
                CreatedAt = DateTime.UtcNow
            };
            _context.SavedVenues.Add(existing);
        }
        await _context.SaveChangesAsync();

        // Calculate accurate real job count
        existing.UsageCount = await _context.EventJobs.CountAsync(j =>
            j.LocationName.ToLower() == existing.Name.ToLower() ||
            (j.Latitude >= minLat && j.Latitude <= maxLat && j.Longitude >= minLng && j.Longitude <= maxLng));

        await _context.SaveChangesAsync();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJob(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == 0) return Unauthorized();

        var job = await _context.EventJobs
            .Include(j => j.Assignments)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null) return NotFound();

        bool canDelete = job.OwnerUserId == currentUserId || job.CreatedByUserId == currentUserId;

        if (!canDelete)
        {
            return Forbid();
        }

        var jobLat = job.Latitude;
        var jobLng = job.Longitude;
        var jobLocation = job.LocationName;

        _context.EventJobs.Remove(job);
        await _context.SaveChangesAsync();

        // Sync venue usage count
        var minLat = jobLat - 0.0008m;
        var maxLat = jobLat + 0.0008m;
        var minLng = jobLng - 0.0008m;
        var maxLng = jobLng + 0.0008m;

        var venue = await _context.SavedVenues.FirstOrDefaultAsync(v =>
            v.Name.ToLower() == jobLocation.ToLower() ||
            (v.Latitude >= minLat && v.Latitude <= maxLat && v.Longitude >= minLng && v.Longitude <= maxLng));

        if (venue != null)
        {
            venue.UsageCount = await _context.EventJobs.CountAsync(j =>
                j.LocationName.ToLower() == venue.Name.ToLower() ||
                (j.Latitude >= minLat && j.Latitude <= maxLat && j.Longitude >= minLng && j.Longitude <= maxLng));
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    // RULE B: Role-Based Financial Masking in DTOs
    private static JobResponseDto MapToDto(EventJob job, int currentUserId)
    {
        bool isOwner = job.OwnerUserId == currentUserId;
        bool isCreator = job.CreatedByUserId == currentUserId;
        bool isAssigned = job.Assignments.Any(a => a.UserId == currentUserId);
        bool canEdit = isOwner || isCreator;
        var myAssignment = job.Assignments.FirstOrDefault(a => a.UserId == currentUserId);

        var assignmentDtos = job.Assignments.Select(a =>
        {
            // If Owner or Creator: sees all amounts
            // If not Owner/Creator: only sees their own CastAmount, all other crew cast is masked to null
            decimal? cast = isOwner || isCreator || a.UserId == currentUserId ? a.CastAmount : null;

            return new JobAssignmentDto(
                a.Id,
                a.UserId,
                a.User?.Name ?? "Thợ",
                a.User?.AvatarUrl,
                cast,
                a.IsPaid,
                a.PersonalNote
            );
        }).ToList();

        // If not Owner/Creator: Force ClientTotalRevenue = null
        decimal? maskedTotalRevenue = isOwner || isCreator ? job.ClientTotalRevenue : null;

        return new JobResponseDto(
            job.Id,
            job.CreatedByUserId,
            job.CreatedByUser?.Name ?? "Unknown",
            job.OwnerUserId,
            job.OwnerUser?.Name,
            job.ExternalClientName,
            job.EventDate.ToString("yyyy-MM-dd"),
            job.StartTime.ToString("HH:mm"),
            job.EndTime.ToString("HH:mm"),
            job.ServiceCode,
            job.LocationName,
            job.SubAddress,
            job.Latitude,
            job.Longitude,
            maskedTotalRevenue,
            job.ExternalWorkersNote,
            job.Note,
            assignmentDtos,
            isOwner,
            isAssigned,
            canEdit,
            myAssignment?.CastAmount,
            myAssignment?.IsPaid
        );
    }
}
