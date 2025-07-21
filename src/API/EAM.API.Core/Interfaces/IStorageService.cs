using EAM.API.Core.Models.Requests;
using EAM.API.Core.Models.Responses;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para serviço de armazenamento (MinIO)
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Gera URL pré-assinada para upload de screenshot
    /// </summary>
    /// <param name="request">Requisição de upload</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL pré-assinada para upload</returns>
    Task<UploadUrlResponse> GenerateUploadUrlAsync(UploadUrlRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma o upload de um arquivo
    /// </summary>
    /// <param name="uploadId">ID do upload</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Confirmação do upload</returns>
    Task<UploadConfirmationResponse> ConfirmUploadAsync(Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém URL pré-assinada para download de um arquivo
    /// </summary>
    /// <param name="objectKey">Chave do objeto</param>
    /// <param name="expiryMinutes">Tempo de expiração em minutos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL pré-assinada para download</returns>
    Task<string> GenerateDownloadUrlAsync(string objectKey, int expiryMinutes = 60, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se um arquivo existe
    /// </summary>
    /// <param name="objectKey">Chave do objeto</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se existe</returns>
    Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém informações de um objeto
    /// </summary>
    /// <param name="objectKey">Chave do objeto</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações do objeto</returns>
    Task<ObjectInfo?> GetObjectInfoAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um objeto
    /// </summary>
    /// <param name="objectKey">Chave do objeto</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    Task<bool> DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove múltiplos objetos
    /// </summary>
    /// <param name="objectKeys">Lista de chaves dos objetos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da remoção</returns>
    Task<BulkDeleteResult> DeleteObjectsAsync(IEnumerable<string> objectKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista objetos por prefixo
    /// </summary>
    /// <param name="prefix">Prefixo dos objetos</param>
    /// <param name="maxResults">Número máximo de resultados</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de objetos</returns>
    Task<IEnumerable<ObjectInfo>> ListObjectsAsync(string prefix, int maxResults = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas de armazenamento
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de armazenamento</returns>
    Task<StorageStatistics> GetStorageStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Configura política de retenção para um bucket
    /// </summary>
    /// <param name="bucketName">Nome do bucket</param>
    /// <param name="retentionDays">Dias de retenção</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se configurado com sucesso</returns>
    Task<bool> SetRetentionPolicyAsync(string bucketName, int retentionDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um bucket se não existir
    /// </summary>
    /// <param name="bucketName">Nome do bucket</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se criado ou já existe</returns>
    Task<bool> EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera chave única para um objeto
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="fileName">Nome do arquivo</param>
    /// <param name="timestamp">Timestamp</param>
    /// <returns>Chave única</returns>
    string GenerateObjectKey(Guid agentId, string fileName, DateTime timestamp);
}

/// <summary>
/// Informações de um objeto no storage
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
    /// Tamanho em bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Tipo de conteúdo
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data de última modificação
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// ETag do objeto
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// Metadados do objeto
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Resultado de remoção em lote
/// </summary>
public class BulkDeleteResult
{
    /// <summary>
    /// Objetos removidos com sucesso
    /// </summary>
    public string[] DeletedObjects { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Objetos que falharam na remoção
    /// </summary>
    public Dictionary<string, string> FailedObjects { get; set; } = new();

    /// <summary>
    /// Indica se todos foram removidos
    /// </summary>
    public bool AllSuccessful => FailedObjects.Count == 0;
}

/// <summary>
/// Estatísticas de armazenamento
/// </summary>
public class StorageStatistics
{
    /// <summary>
    /// Número total de objetos
    /// </summary>
    public long TotalObjects { get; set; }

    /// <summary>
    /// Tamanho total usado em bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Estatísticas por bucket
    /// </summary>
    public Dictionary<string, BucketStatistics> BucketStatistics { get; set; } = new();

    /// <summary>
    /// Estatísticas por tipo de conteúdo
    /// </summary>
    public Dictionary<string, ContentTypeStatistics> ContentTypeStatistics { get; set; } = new();
}

/// <summary>
/// Estatísticas de bucket
/// </summary>
public class BucketStatistics
{
    /// <summary>
    /// Nome do bucket
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Número de objetos
    /// </summary>
    public long ObjectCount { get; set; }

    /// <summary>
    /// Tamanho total em bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Estatísticas por tipo de conteúdo
/// </summary>
public class ContentTypeStatistics
{
    /// <summary>
    /// Tipo de conteúdo
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Número de objetos
    /// </summary>
    public long ObjectCount { get; set; }

    /// <summary>
    /// Tamanho total em bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Tamanho médio em bytes
    /// </summary>
    public long AverageSizeBytes { get; set; }
}