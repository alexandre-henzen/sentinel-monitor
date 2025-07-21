using System.ComponentModel.DataAnnotations;

namespace EAM.API.Core.Models.Requests;

/// <summary>
/// Requisição para obter URL de upload de screenshot
/// </summary>
public class UploadUrlRequest
{
    /// <summary>
    /// ID do agente que solicita o upload
    /// </summary>
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// Nome do arquivo do screenshot
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Tamanho do arquivo em bytes
    /// </summary>
    [Required]
    [Range(1, 50_000_000)] // Max 50MB
    public long FileSize { get; set; }

    /// <summary>
    /// Tipo MIME do arquivo
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Hash MD5 do arquivo para verificação de integridade
    /// </summary>
    [MaxLength(32)]
    public string? FileHash { get; set; }

    /// <summary>
    /// Largura da imagem em pixels
    /// </summary>
    [Range(1, 7680)] // Max 8K width
    public int? Width { get; set; }

    /// <summary>
    /// Altura da imagem em pixels
    /// </summary>
    [Range(1, 4320)] // Max 8K height
    public int? Height { get; set; }

    /// <summary>
    /// Qualidade da compressão (1-100)
    /// </summary>
    [Range(1, 100)]
    public int? Quality { get; set; }

    /// <summary>
    /// Timestamp da captura do screenshot
    /// </summary>
    [Required]
    public DateTime CaptureTimestamp { get; set; }

    /// <summary>
    /// Aplicação em foco durante a captura
    /// </summary>
    [MaxLength(256)]
    public string? ForegroundApplication { get; set; }

    /// <summary>
    /// Título da janela em foco
    /// </summary>
    [MaxLength(512)]
    public string? ForegroundWindowTitle { get; set; }

    /// <summary>
    /// Indica se contém conteúdo sensível
    /// </summary>
    public bool ContainsSensitiveContent { get; set; }

    /// <summary>
    /// Metadados adicionais
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}