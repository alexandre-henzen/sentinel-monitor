using System.ComponentModel.DataAnnotations;

namespace EAM.API.Core.Models.Responses;

/// <summary>
/// Resposta com URL de upload para screenshot
/// </summary>
public class UploadUrlResponse
{
    /// <summary>
    /// ID único do upload
    /// </summary>
    public Guid UploadId { get; set; }

    /// <summary>
    /// URL pré-assinada para upload
    /// </summary>
    [Required]
    public string UploadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Método HTTP para upload (PUT, POST)
    /// </summary>
    [Required]
    public string HttpMethod { get; set; } = "PUT";

    /// <summary>
    /// Headers necessários para o upload
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Parâmetros de formulário (para POST multipart)
    /// </summary>
    public Dictionary<string, string>? FormFields { get; set; }

    /// <summary>
    /// Data de expiração da URL
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Tempo limite para upload em segundos
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Tamanho máximo permitido para upload
    /// </summary>
    public long MaxFileSize { get; set; }

    /// <summary>
    /// Tipos de conteúdo aceitos
    /// </summary>
    public string[] AcceptedContentTypes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Bucket de destino no MinIO
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Chave do objeto no storage
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// URL para callback após upload bem-sucedido
    /// </summary>
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Metadados que serão anexados ao objeto
    /// </summary>
    public Dictionary<string, string>? ObjectMetadata { get; set; }

    /// <summary>
    /// Instruções adicionais para o upload
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Indica se o upload deve ser feito em partes (multipart)
    /// </summary>
    public bool IsMultipart { get; set; }

    /// <summary>
    /// Tamanho de cada parte para upload multipart
    /// </summary>
    public long? PartSize { get; set; }

    /// <summary>
    /// ID da sessão de upload multipart
    /// </summary>
    public string? MultipartUploadId { get; set; }
}

/// <summary>
/// Resposta para confirmação de upload
/// </summary>
public class UploadConfirmationResponse
{
    /// <summary>
    /// ID do upload
    /// </summary>
    public Guid UploadId { get; set; }

    /// <summary>
    /// Indica se o upload foi bem-sucedido
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensagem de status
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// URL pública do arquivo (se disponível)
    /// </summary>
    public string? PublicUrl { get; set; }

    /// <summary>
    /// Hash MD5 do arquivo armazenado
    /// </summary>
    public string? StoredFileHash { get; set; }

    /// <summary>
    /// Tamanho do arquivo armazenado
    /// </summary>
    public long StoredFileSize { get; set; }

    /// <summary>
    /// Timestamp do upload
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Data de expiração do arquivo
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Metadados adicionais do upload
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}