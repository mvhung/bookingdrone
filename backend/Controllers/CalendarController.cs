using EventMap.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EventMap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly AppDbContext _context;

    public CalendarController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// iCalendar (RFC 5545) subscription feed for Google Calendar / Apple Calendar
    /// Users can subscribe by adding this URL to Google Calendar ("From URL")
    /// </summary>
    [HttpGet("feed/{userId}")]
    [HttpGet("ical/{userId}")]
    public async Task<IActionResult> GetUserCalendarFeed(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return NotFound("User not found");
        }

        // Fetch all jobs where user is Owner or Assigned crew
        var jobs = await _context.EventJobs
            .Include(j => j.OwnerUser)
            .Include(j => j.CreatedByUser)
            .Include(j => j.Assignments)
                .ThenInclude(a => a.User)
            .Where(j => j.OwnerUserId == userId || j.Assignments.Any(a => a.UserId == userId))
            .OrderBy(j => j.EventDate)
            .ThenBy(j => j.StartTime)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//EventMap Crew Scheduler//VN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine($"X-WR-CALNAME:Lịch Chạy Show - {user.Name}");
        sb.AppendLine("X-WR-TIMEZONE:Asia/Ho_Chi_Minh");

        foreach (var job in jobs)
        {
            var dateStr = job.EventDate.ToString("yyyyMMdd");
            var startStr = job.StartTime.ToString("hhmmss");
            var endStr = job.EndTime.ToString("hhmmss");

            var crewNames = string.Join(", ", job.Assignments.Select(a => a.User?.Name ?? $"Thợ #{a.UserId}"));
            if (!string.IsNullOrWhiteSpace(job.ExternalWorkersNote))
            {
                crewNames += (string.IsNullOrEmpty(crewNames) ? "" : ", ") + job.ExternalWorkersNote;
            }

            var summary = $"[{job.ServiceCode}] {job.LocationName}";
            var fullLocation = string.IsNullOrEmpty(job.SubAddress) 
                ? job.LocationName 
                : $"{job.LocationName}, {job.SubAddress}";

            var descLines = new List<string?>
            {
                $"Dịch vụ: {job.ServiceCode}",
                $"Địa điểm: {job.LocationName}",
                string.IsNullOrEmpty(job.SubAddress) ? null : $"Địa chỉ: {job.SubAddress}",
                $"Chủ Job: {job.OwnerUser?.Name ?? job.CreatedByUser?.Name ?? "Khách"}",
                string.IsNullOrEmpty(crewNames) ? null : $"Đội ngũ: {crewNames}",
                string.IsNullOrEmpty(job.Note) ? null : $"Ghi chú: {job.Note}"
            };

            var description = string.Join("\\n", descLines.Where(x => !string.IsNullOrEmpty(x)));

            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:eventmap-job-{job.Id}-{dateStr}@eventmap.local");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd\\THHmmss\\Z}");
            sb.AppendLine($"DTSTART:{dateStr}T{startStr}");
            sb.AppendLine($"DTEND:{dateStr}T{endStr}");
            sb.AppendLine($"SUMMARY:{EscapeIcs(summary)}");
            sb.AppendLine($"LOCATION:{EscapeIcs(fullLocation)}");
            sb.AppendLine($"DESCRIPTION:{EscapeIcs(description)}");
            sb.AppendLine("STATUS:CONFIRMED");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/calendar; charset=utf-8", $"Lich_Chay_{user.Name}.ics");
    }

    private static string EscapeIcs(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,");
    }
}
