namespace EventMap.Api.Models;

public class SavedVenue
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SubAddress { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int UsageCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
