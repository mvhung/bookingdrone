namespace EventMap.Api.Models;

public class ServiceType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. "BUTTERFLY", "FPV"
    public string Name { get; set; } = string.Empty; // e.g. "Bướm", "Flycam"
    public string Icon { get; set; } = "🎯";          // Emoji or icon string
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
}
