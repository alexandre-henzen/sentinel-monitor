using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EAM.Shared.DTOs;

public class ScreenshotDto
{
    [Required]
    public Guid AgentId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string Base64Data { get; set; } = string.Empty;
    
    public int Quality { get; set; } = 75;
    
    public int Width { get; set; }
    
    public int Height { get; set; }
    
    public long SizeBytes { get; set; }
    
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, object>? Metadata { get; set; }
}

public class ScreenshotUploadDto
{
    [Required]
    public Guid AgentId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    
    public int Quality { get; set; } = 75;
    
    public int Width { get; set; }
    
    public int Height { get; set; }
    
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, object>? Metadata { get; set; }
}