using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.Agent.Core.Models;

/// <summary>
/// Evento específico de captura de screenshot
/// </summary>
[Table("ScreenshotEvents")]
public class ScreenshotEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID do evento de atividade relacionado
    /// </summary>
    [Required]
    public Guid ActivityEventId { get; set; }

    /// <summary>
    /// Nome do arquivo do screenshot
    /// </summary>
    [MaxLength(256)]
    [Required]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Caminho local do arquivo
    /// </summary>
    [MaxLength(512)]
    [Required]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Hash MD5 do arquivo para verificação de integridade
    /// </summary>
    [MaxLength(32)]
    public string? FileHash { get; set; }

    /// <summary>
    /// Largura da imagem em pixels
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Altura da imagem em pixels
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Tamanho do arquivo em bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Qualidade da compressão JPEG (1-100)
    /// </summary>
    public int Quality { get; set; }

    /// <summary>
    /// Formato da imagem
    /// </summary>
    public ImageFormat Format { get; set; }

    /// <summary>
    /// Aplicação que estava em foco durante o screenshot
    /// </summary>
    [MaxLength(256)]
    public string? ForegroundApplication { get; set; }

    /// <summary>
    /// Título da janela em foco
    /// </summary>
    [MaxLength(512)]
    public string? ForegroundWindowTitle { get; set; }

    /// <summary>
    /// Indica se contém conteúdo sensível detectado
    /// </summary>
    public bool ContainsSensitiveContent { get; set; }

    /// <summary>
    /// Indica se o screenshot foi processado/analisado
    /// </summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    /// Indica se foi enviado para o servidor
    /// </summary>
    public bool IsUploaded { get; set; }

    /// <summary>
    /// Data de upload para o servidor
    /// </summary>
    public DateTime? UploadedAt { get; set; }

    /// <summary>
    /// Timestamp do evento
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Relacionamento com ActivityEvent
    /// </summary>
    [ForeignKey(nameof(ActivityEventId))]
    public ActivityEvent ActivityEvent { get; set; } = null!;
}

/// <summary>
/// Formatos de imagem suportados
/// </summary>
public enum ImageFormat
{
    JPEG = 1,
    PNG = 2,
    BMP = 3,
    GIF = 4,
    TIFF = 5
}