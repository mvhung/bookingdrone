using System.Text.Json.Serialization;

namespace EventMap.Api.Models;

public class UserBusyDate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly BusyDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Reason { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
}
