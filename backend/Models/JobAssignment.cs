using System.Text.Json.Serialization;

namespace EventMap.Api.Models;

public class JobAssignment
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int UserId { get; set; }
    
    // Payout for this specific member
    public decimal CastAmount { get; set; }
    
    // Payment status: Waiting (false) vs Paid (true)
    public bool IsPaid { get; set; } = false;
    
    public string PersonalNote { get; set; } = string.Empty;

    // Navigation
    [JsonIgnore]
    public EventJob? Job { get; set; }
    public User? User { get; set; }
}
