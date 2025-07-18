using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EAM.Shared.Models.Entities;

public class ActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid AgentId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? ApplicationName { get; set; }
    
    [MaxLength(500)]
    public string? WindowTitle { get; set; }
    
    [MaxLength(2000)]
    public string? Url { get; set; }
    
    [MaxLength(255)]
    public string? ProcessName { get; set; }
    
    public int? ProcessId { get; set; }
    
    public int? DurationSeconds { get; set; }
    
    public int? ProductivityScore { get; set; }
    
    public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;
    
    [MaxLength(255)]
    public string? ScreenshotPath { get; set; }
    
    public Dictionary<string, object>? Metadata { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Agent? Agent { get; set; }
}