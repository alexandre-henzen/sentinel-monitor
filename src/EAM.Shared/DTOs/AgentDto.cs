using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EAM.Shared.Enums;

namespace EAM.Shared.DTOs;

public class AgentDto
{
    public Guid Id { get; set; }
    
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
    
    [JsonPropertyName("last_seen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    public AgentStatus Status { get; set; } = AgentStatus.Active;
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}