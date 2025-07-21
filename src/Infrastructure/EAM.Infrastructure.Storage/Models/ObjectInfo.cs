namespace EAM.Infrastructure.Storage.Models;

/// <summary>
/// Informações sobre um objeto no armazenamento
/// </summary>
public class ObjectInfo
{
    /// <summary>
    /// Chave do objeto
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Nome do bucket
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Tamanho do objeto em bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Tipo de conteúdo MIME
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última modificação
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// ETag do objeto
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Metadados do objeto
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}