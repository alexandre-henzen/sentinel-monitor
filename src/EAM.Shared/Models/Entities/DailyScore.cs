using System.ComponentModel.DataAnnotations;

namespace EAM.Shared.Models.Entities;

public class DailyScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid AgentId { get; set; }
    
    [Required]
    public DateOnly ActivityDate { get; set; }
    
    public decimal AvgProductivity { get; set; }
    
    public int TotalActiveSeconds { get; set; }
    
    public int TotalEvents { get; set; }
    
    public int UniqueApplications { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Agent? Agent { get; set; }
}