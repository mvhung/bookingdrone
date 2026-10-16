using EventMap.Api.Data;
using EventMap.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EventMap.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AvailabilityController : ControllerBase
{
    private readonly AppDbContext _context;

    public AvailabilityController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<AvailabilityItemDto>>> CheckAvailability(
        [FromQuery] string date,
        [FromQuery] string start,
        [FromQuery] string end,
        [FromQuery] string service,
        [FromQuery] int? ignoreJobId = null)
    {
        if (!DateOnly.TryParse(date, out var eventDate))
            return BadRequest(new { message = "Invalid date format (yyyy-MM-dd)" });

        if (!TimeOnly.TryParse(start, out var startTime) || !TimeOnly.TryParse(end, out var endTime))
            return BadRequest(new { message = "Invalid time format (HH:mm)" });

        if (string.IsNullOrWhiteSpace(service))
            return BadRequest(new { message = "Service is required" });

        // 1. Fetch users with busy dates and job assignments on that date
        var allUsers = await _context.Users
            .Include(u => u.BusyDates)
            .Include(u => u.Assignments)
                .ThenInclude(a => a.Job)
            .AsNoTracking()
            .ToListAsync();

        var dayOfWeek = eventDate.DayOfWeek;
        bool isWeekday = dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Friday;

        var results = new List<AvailabilityItemDto>();

        foreach (var user in allUsers)
        {
            // Check if user provides this service
            var userServices = JsonSerializer.Deserialize<List<string>>(user.ServicesJson) ?? new List<string>();
            if (!userServices.Contains(service, StringComparer.OrdinalIgnoreCase))
            {
                continue; // User does not offer this service
            }

            bool isBusy = false;
            string busyType = "FREE";
            string busyLabel = "Đang rảnh";

            // 1. Check Day Off in UserBusyDates
            var busyDate = user.BusyDates.FirstOrDefault(b => b.BusyDate == eventDate);
            if (busyDate != null)
            {
                // If busyDate has specific times, check overlap, otherwise whole day off
                if (!busyDate.StartTime.HasValue && !busyDate.EndTime.HasValue)
                {
                    isBusy = true;
                    busyType = "DAY_OFF";
                    busyLabel = "Báo nghỉ";
                }
                else
                {
                    var bStart = busyDate.StartTime ?? TimeOnly.MinValue;
                    var bEnd = busyDate.EndTime ?? TimeOnly.MaxValue;
                    if (IsTimeOverlap(startTime, endTime, bStart, bEnd))
                    {
                        isBusy = true;
                        busyType = "DAY_OFF";
                        busyLabel = "Báo nghỉ";
                    }
                }
            }

            // 2. Check Mon-Fri Office hours
            if (!isBusy && user.OfficeBusyEnabled && isWeekday)
            {
                if (IsTimeOverlap(startTime, endTime, user.OfficeBusyStart, user.OfficeBusyEnd))
                {
                    isBusy = true;
                    busyType = "OFFICE_HOURS";
                    busyLabel = "Bận giờ HC";
                }
            }

            // 3. Check conflicting jobs
            if (!isBusy)
            {
                var conflictingJob = user.Assignments
                    .Select(a => a.Job)
                    .Where(j => j != null && j.Id != ignoreJobId && j.EventDate == eventDate)
                    .FirstOrDefault(j => IsTimeOverlap(startTime, endTime, j!.StartTime, j.EndTime));

                if (conflictingJob != null)
                {
                    isBusy = true;
                    busyType = "CONFLICT_JOB";
                    busyLabel = "Trùng show";
                }
            }

            // Privacy Guarantee: Never expose where or who they are working for
            results.Add(new AvailabilityItemDto(
                user.Id,
                user.Name,
                user.AvatarUrl,
                isBusy,
                busyType,
                busyLabel
            ));
        }

        return Ok(results);
    }

    private static bool IsTimeOverlap(TimeOnly s1, TimeOnly e1, TimeOnly s2, TimeOnly e2)
    {
        return !(e1 <= s2 || s1 >= e2);
    }
}
