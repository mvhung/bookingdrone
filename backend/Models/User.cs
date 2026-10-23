using System.Text.Json.Serialization;

namespace EventMap.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    
    // JSON serialized array of service codes, e.g. ["BUTTERFLY", "FPV", "FLYCAM"]
    public string ServicesJson { get; set; } = "[]";

    // Admin flag for managing users and service types
    public bool IsAdmin { get; set; } = false;

    // Fixed office job settings (e.g. Mon-Fri)
    public bool OfficeBusyEnabled { get; set; } = false;
    public TimeOnly OfficeBusyStart { get; set; } = new(8, 0);
    public TimeOnly OfficeBusyEnd { get; set; } = new(17, 30);

    // Navigations
    [JsonIgnore]
    public ICollection<EventJob> JobsCreated { get; set; } = new List<EventJob>();
    
    [JsonIgnore]
    public ICollection<EventJob> JobsOwned { get; set; } = new List<EventJob>();
    
    [JsonIgnore]
    public ICollection<JobAssignment> Assignments { get; set; } = new List<JobAssignment>();
    
    [JsonIgnore]
    public ICollection<UserBusyDate> BusyDates { get; set; } = new List<UserBusyDate>();
}
