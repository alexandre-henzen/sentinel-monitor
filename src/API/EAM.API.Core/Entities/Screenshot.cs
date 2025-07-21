using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.API.Core.Entities;

/// <summary>
/// Entidade representando um screenshot capturado pelo agente
/// </summary>
[Table("screenshots")]
public class Screenshot
{
    /// <summary>
    /// ID único do screenshot
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// ID do agente que capturou o screenshot
    /// </summary>
    [Column("agent_id")]
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// ID do usuário
    /// </summary>
    [Column("user_id")]
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// ID do evento de atividade relacionado (se houver)
    /// </summary>
    [Column("activity_event_id")]
    public Guid? ActivityEventId { get; set; }

    /// <summary>
    /// Timestamp da captura
    /// </summary>
    [Column("timestamp")]
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Nome do arquivo original
    /// </summary>
    [Column("file_name")]
    [Required]
    [MaxLength(256)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Caminho/chave do arquivo no storage
    /// </summary>
    [Column("file_path")]
    [Required]
    [MaxLength(512)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Bucket do MinIO onde está armazenado
    /// </summary>
    [Column("bucket_name")]
    [Required]
    [MaxLength(128)]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Chave do objeto no MinIO
    /// </summary>
    [Column("object_key")]
    [Required]
    [MaxLength(512)]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Hash MD5 do arquivo
    /// </summary>
    [Column("file_hash")]
    [MaxLength(32)]
    public string? FileHash { get; set; }

    /// <summary>
    /// Tamanho do arquivo em bytes
    /// </summary>
    [Column("file_size")]
    public long FileSize { get; set; }

    /// <summary>
    /// Largura da imagem em pixels
    /// </summary>
    [Column("width")]
    public int Width { get; set; }

    /// <summary>
    /// Altura da imagem em pixels
    /// </summary>
    [Column("height")]
    public int Height { get; set; }

    /// <summary>
    /// Formato da imagem (JPEG, PNG, etc.)
    /// </summary>
    [Column("format")]
    [Required]
    [MaxLength(32)]
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Qualidade da compressão (1-100)
    /// </summary>
    [Column("quality")]
    public int Quality { get; set; }

    /// <summary>
    /// Tipo MIME do arquivo
    /// </summary>
    [Column("content_type")]
    [Required]
    [MaxLength(64)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Aplicação em foco durante a captura
    /// </summary>
    [Column("foreground_application")]
    [MaxLength(256)]
    public string? ForegroundApplication { get; set; }

    /// <summary>
    /// Título da janela em foco
    /// </summary>
    [Column("foreground_window_title")]
    [MaxLength(512)]
    public string? ForegroundWindowTitle { get; set; }

    /// <summary>
    /// Indica se contém conteúdo sensível
    /// </summary>
    [Column("contains_sensitive_content")]
    public bool ContainsSensitiveContent { get; set; }

    /// <summary>
    /// Indica se foi processado/analisado
    /// </summary>
    [Column("is_processed")]
    public bool IsProcessed { get; set; }

    /// <summary>
    /// Indica se foi feito upload com sucesso
    /// </summary>
    [Column("is_uploaded")]
    public bool IsUploaded { get; set; }

    /// <summary>
    /// Data do upload
    /// </summary>
    [Column("uploaded_at")]
    public DateTime? UploadedAt { get; set; }

    /// <summary>
    /// Data de expiração do arquivo
    /// </summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// URL pública do arquivo (se disponível)
    /// </summary>
    [Column("public_url")]
    [MaxLength(1024)]
    public string? PublicUrl { get; set; }

    /// <summary>
    /// URL de thumbnail (se disponível)
    /// </summary>
    [Column("thumbnail_url")]
    [MaxLength(1024)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Metadados adicionais em JSON
    /// </summary>
    [Column("metadata")]
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }

    /// <summary>
    /// Status do processamento
    /// </summary>
    [Column("processing_status")]
    [Required]
    public ScreenshotProcessingStatus ProcessingStatus { get; set; } = ScreenshotProcessingStatus.Pending;

    /// <summary>
    /// Mensagem de erro (se houver)
    /// </summary>
    [Column("error_message")]
    [MaxLength(512)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Número de tentativas de processamento
    /// </summary>
    [Column("retry_count")]
    public int RetryCount { get; set; }

    /// <summary>
    /// Versão do agente que capturou
    /// </summary>
    [Column("agent_version")]
    [MaxLength(32)]
    public string? AgentVersion { get; set; }

    /// <summary>
    /// Timestamp de criação no banco
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp de última atualização
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Relacionamento com agente
    /// </summary>
    [ForeignKey(nameof(AgentId))]
    public virtual Agent Agent { get; set; } = null!;

    /// <summary>
    /// Relacionamento com evento de atividade
    /// </summary>
    [ForeignKey(nameof(ActivityEventId))]
    public virtual ActivityEvent? ActivityEvent { get; set; }
}

/// <summary>
/// Status do processamento do screenshot
/// </summary>
public enum ScreenshotProcessingStatus
{
    /// <summary>
    /// Pendente para processamento
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Processando
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Processado com sucesso
    /// </summary>
    Processed = 3,

    /// <summary>
    /// Erro no processamento
    /// </summary>
    Error = 4,

    /// <summary>
    /// Rejeitado (conteúdo sensível)
    /// </summary>
    Rejected = 5,

    /// <summary>
    /// Expirado
    /// </summary>
    Expired = 6
}

/// <summary>
/// Formatos de imagem suportados
/// </summary>
public static class SupportedImageFormats
{
    public const string JPEG = "JPEG";
    public const string PNG = "PNG";
    public const string BMP = "BMP";
    public const string GIF = "GIF";
    public const string TIFF = "TIFF";
    public const string WEBP = "WEBP";
}

/// <summary>
/// Tipos MIME suportados
/// </summary>
public static class SupportedContentTypes
{
    public const string JPEG = "image/jpeg";
    public const string PNG = "image/png";
    public const string BMP = "image/bmp";
    public const string GIF = "image/gif";
    public const string TIFF = "image/tiff";
    public const string WEBP = "image/webp";
}