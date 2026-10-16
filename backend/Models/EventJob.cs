using System.Text.Json.Serialization;

namespace EventMap.Api.Models;

public class EventJob
{
    public int Id { get; set; }
    
    public int CreatedByUserId { get; set; }
    public int? OwnerUserId { get; set; }
    
    public string? ExternalClientName { get; set; }
    
    public DateOnly EventDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    
    // BUTTERFLY, FPV, FLYCAM, CAMERA, PHOTO
    public string ServiceCode { get; set; } = string.Empty;
    
    // Serves as primary Job Title
    public string LocationName { get; set; } = string.Empty;
    public string SubAddress { get; set; } = string.Empty;
    
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    
    // Nullable so crew member can create first, owner fills in later
    public decimal? ClientTotalRevenue { get; set; }
    
    // Comma-separated names of external non-user freelancers
    public string ExternalWorkersNote { get; set; } = string.Empty;
    
    public string Note { get; set; } = string.Empty;

    // Navigation
    public User? CreatedByUser { get; set; }
    public User? OwnerUser { get; set; }
    public ICollection<JobAssignment> Assignments { get; set; } = new List<JobAssignment>();
}
