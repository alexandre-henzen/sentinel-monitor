using System.ComponentModel.DataAnnotations;
using EAM.Shared.Enums;

namespace EAM.Shared.Models.Entities;

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(255)]
    public string MachineId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string MachineName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string UserName { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? OsVersion { get; set; }
    
    [MaxLength(50)]
    public string? AgentVersion { get; set; }
    
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    public AgentStatus Status { get; set; } = AgentStatus.Active;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public virtual ICollection<DailyScore> DailyScores { get; set; } = new List<DailyScore>();
}