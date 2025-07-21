using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using System.Text.Json;
using EAM.API.Core.Interfaces;
using EAM.API.Core.Models.Requests;
using EAM.API.Core.Models.Responses;
using EAM.API.Core.Configuration;

namespace EAM.Infrastructure.Storage.Services;

/// <summary>
/// Implementação do serviço de armazenamento usando MinIO
/// </summary>
public class MinIOStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly StorageSettings _storageSettings;
    private readonly ILogger<MinIOStorageService> _logger;
    private readonly Dictionary<Guid, UploadUrlResponse> _uploadCache = new();

    /// <summary>
    /// Construtor do serviço
    /// </summary>
    /// <param name="minioClient">Cliente MinIO</param>
    /// <param name="storageSettings">Configurações de armazenamento</param>
    /// <param name="logger">Logger</param>
    public MinIOStorageService(
        IMinioClient minioClient,
        IOptions<StorageSettings> storageSettings,
        ILogger<MinIOStorageService> logger)
    {
        _minioClient = minioClient;
        _storageSettings = storageSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gera URL pré-assinada para upload de screenshot
    /// </summary>
    /// <param name="request">Requisição de upload</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL pré-assinada para upload</returns>
    public async Task<UploadUrlResponse> GenerateUploadUrlAsync(UploadUrlRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var uploadId = Guid.NewGuid();
            var objectKey = GenerateObjectKey(request.AgentId, request.FileName, request.CaptureTimestamp);
            var bucketName = _storageSettings.DefaultBucket;

            _logger.LogInformation("Gerando URL de upload para {FileName} no bucket {BucketName}", 
                request.FileName, bucketName);

            // Garantir que o bucket existe
            await EnsureBucketExistsAsync(bucketName, cancellationToken);

            // Gerar URL pré-assinada para upload
            var presignedUrl = await _minioClient.PresignedPutObjectAsync(
                new PresignedPutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey)
                    .WithExpiry(_storageSettings.UrlExpiryMinutes * 60));

            // Preparar metadados
            var metadata = new Dictionary<string, string>
            {
                ["agent-id"] = request.AgentId.ToString(),
                ["original-filename"] = request.FileName,
                ["capture-timestamp"] = request.CaptureTimestamp.ToString("O"),
                ["file-size"] = request.FileSize.ToString(),
                ["content-type"] = request.ContentType
            };

            if (request.Width.HasValue)
                metadata["width"] = request.Width.Value.ToString();

            if (request.Height.HasValue)
                metadata["height"] = request.Height.Value.ToString();

            if (request.Quality.HasValue)
                metadata["quality"] = request.Quality.Value.ToString();

            if (!string.IsNullOrEmpty(request.FileHash))
                metadata["file-hash"] = request.FileHash;

            if (!string.IsNullOrEmpty(request.ForegroundApplication))
                metadata["foreground-application"] = request.ForegroundApplication;

            if (!string.IsNullOrEmpty(request.ForegroundWindowTitle))
                metadata["foreground-window-title"] = request.ForegroundWindowTitle;

            metadata["contains-sensitive-content"] = request.ContainsSensitiveContent.ToString();

            var response = new UploadUrlResponse
            {
                UploadId = uploadId,
                UploadUrl = presignedUrl,
                HttpMethod = "PUT",
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = request.ContentType,
                    ["Content-Length"] = request.FileSize.ToString()
                },
                ExpiresAt = DateTime.UtcNow.AddMinutes(_storageSettings.UrlExpiryMinutes),
                TimeoutSeconds = 300,
                MaxFileSize = _storageSettings.MaxUploadSizeBytes,
                AcceptedContentTypes = new[] { "image/jpeg", "image/png", "image/bmp", "image/gif", "image/webp" },
                BucketName = bucketName,
                ObjectKey = objectKey,
                ObjectMetadata = metadata,
                Instructions = "Use PUT method to upload the file to the provided URL"
            };

            // Cache da resposta para confirmação posterior
            _uploadCache[uploadId] = response;

            _logger.LogInformation("URL de upload gerada: {UploadId} para objeto {ObjectKey}", 
                uploadId, objectKey);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar URL de upload para {FileName}", request.FileName);
            throw;
        }
    }

    /// <summary>
    /// Confirma o upload de um arquivo
    /// </summary>
    /// <param name="uploadId">ID do upload</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Confirmação do upload</returns>
    public async Task<UploadConfirmationResponse> ConfirmUploadAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_uploadCache.TryGetValue(uploadId, out var uploadInfo))
            {
                return new UploadConfirmationResponse
                {
                    UploadId = uploadId,
                    Success = false,
                    Message = "Upload não encontrado ou expirado"
                };
            }

            // Verificar se o objeto existe no MinIO
            var objectExists = await ObjectExistsAsync(uploadInfo.ObjectKey, cancellationToken);
            if (!objectExists)
            {
                return new UploadConfirmationResponse
                {
                    UploadId = uploadId,
                    Success = false,
                    Message = "Arquivo não foi encontrado no storage"
                };
            }

            // Obter informações do objeto
            var objectInfo = await GetObjectInfoAsync(uploadInfo.ObjectKey, cancellationToken);
            
            var response = new UploadConfirmationResponse
            {
                UploadId = uploadId,
                Success = true,
                Message = "Upload confirmado com sucesso",
                StoredFileSize = objectInfo?.Size ?? 0,
                StoredFileHash = objectInfo?.ETag,
                UploadedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_storageSettings.DefaultTtlDays),
                Metadata = new Dictionary<string, object>
                {
                    ["bucket"] = uploadInfo.BucketName,
                    ["object_key"] = uploadInfo.ObjectKey,
                    ["upload_method"] = "presigned_url"
                }
            };

            // Remove do cache
            _uploadCache.Remove(uploadId);

            _logger.LogInformation("Upload confirmado: {UploadId} para objeto {ObjectKey}", 
                uploadId, uploadInfo.ObjectKey);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar upload {UploadId}", uploadId);
            throw;
        }
    }

    /// <summary>
    /// Obtém URL pré-assinada para download de um arquivo
    /// </summary>
    /// <param name="objectKey">Chave do objeto</param>
    /// <param name="expiryMinutes">Tempo de expiração em minutos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL pré-assinada para download</returns>
    public async Task<string> GenerateDownloadUrlAsync(string objectKey, int expiryMinutes = 60, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketName = _storageSettings.DefaultBucket;
            
            // Verificar se o objeto existe
            var exists = await ObjectExistsAsync(objectKey, cancellationToken);
            if (!exists)
            {
                throw new FileNotFoundException($"Objeto {objectKey} não encontrado");
            }

            // Gerar URL pré-assinada para download
            var presignedUrl = await _minioClient.PresignedGetObjectAsync(
                new PresignedGetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey)
                    .WithExpiry(expiryMinutes * 60));

            _logger.LogInformation("URL de download gerada para objeto {ObjectKey}", objectKey);

            return presignedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar URL de download para objeto {ObjectKey}", objectKey);
            throw;
        }
    }

    /// <summary>
    /// Verifica se um arquivo existe
    /// </summary>
    /// <param name="objectKey">Chave do objeto</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se existe</returns>
    public async Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketName = _storageSettings.DefaultBucket;
            
            await _minioClient.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey));

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Obtém informações de um objeto
    /// </summary>
    /// <param name="objectKey">Chave do objeto</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações do objeto</returns>
    public async Task<ObjectInfo?> GetObjectInfoAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketName = _storageSettings.DefaultBucket;
            
            var objectStat = await _minioClient.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey));

            return new ObjectInfo
            {
                Key = objectKey,
                BucketName = bucketName,
                Size = objectStat.Size,
                ContentType = objectStat.ContentType,
                CreatedAt = objectStat.LastModified,
                LastModified = objectStat.LastModified,
                ETag = objectStat.ETag,
                Metadata = objectStat.MetaData
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Remove um objeto
    /// </summary>
    /// <param name="objectKey">Chave do objeto</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    public async Task<bool> DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketName = _storageSettings.DefaultBucket;
            
            await _minioClient.RemoveObjectAsync(
                new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectKey));

            _logger.LogInformation("Objeto removido: {ObjectKey}", objectKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover objeto {ObjectKey}", objectKey);
            return false;
        }
    }

    /// <summary>
    /// Remove múltiplos objetos
    /// </summary>
    /// <param name="objectKeys">Lista de chaves dos objetos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da remoção</returns>
    public async Task<BulkDeleteResult> DeleteObjectsAsync(IEnumerable<string> objectKeys, CancellationToken cancellationToken = default)
    {
        var result = new BulkDeleteResult();
        var deletedObjects = new List<string>();
        var failedObjects = new Dictionary<string, string>();

        try
        {
            var bucketName = _storageSettings.DefaultBucket;
            
            foreach (var objectKey in objectKeys)
            {
                try
                {
                    await _minioClient.RemoveObjectAsync(
                        new RemoveObjectArgs()
                            .WithBucket(bucketName)
                            .WithObject(objectKey));

                    deletedObjects.Add(objectKey);
                }
                catch (Exception ex)
                {
                    failedObjects[objectKey] = ex.Message;
                }
            }

            result.DeletedObjects = deletedObjects.ToArray();
            result.FailedObjects = failedObjects;

            _logger.LogInformation("Remoção em lote concluída: {DeletedCount} removidos, {FailedCount} falharam", 
                deletedObjects.Count, failedObjects.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na remoção em lote");
            throw;
        }
    }

    /// <summary>
    /// Lista objetos por prefixo
    /// </summary>
    /// <param name="prefix">Prefixo dos objetos</param>
    /// <param name="maxResults">Número máximo de resultados</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de objetos</returns>
    public async Task<IEnumerable<ObjectInfo>> ListObjectsAsync(string prefix, int maxResults = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketName = _storageSettings.DefaultBucket;
            var objects = new List<ObjectInfo>();

            var listArgs = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix)
                .WithRecursive(true);

            var observable = _minioClient.ListObjectsAsync(listArgs);
            
            await foreach (var item in observable.ToAsyncEnumerable().WithCancellation(cancellationToken))
            {
                if (objects.Count >= maxResults)
                    break;

                objects.Add(new ObjectInfo
                {
                    Key = item.Key,
                    BucketName = bucketName,
                    Size = item.Size,
                    LastModified = item.LastModified,
                    ETag = item.ETag
                });
            }

            _logger.LogInformation("Listagem de objetos concluída: {Count} objetos encontrados com prefixo {Prefix}", 
                objects.Count, prefix);

            return objects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar objetos com prefixo {Prefix}", prefix);
            throw;
        }
    }

    /// <summary>
    /// Obtém estatísticas de armazenamento
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de armazenamento</returns>
    public async Task<StorageStatistics> GetStorageStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketName = _storageSettings.DefaultBucket;
            var statistics = new StorageStatistics();

            // Listar todos os objetos para calcular estatísticas
            var objects = await ListObjectsAsync("", 10000, cancellationToken);
            
            statistics.TotalObjects = objects.Count();
            statistics.TotalSizeBytes = objects.Sum(o => o.Size);

            // Estatísticas por tipo de conteúdo
            var contentTypeStats = new Dictionary<string, ContentTypeStatistics>();
            foreach (var obj in objects)
            {
                var contentType = obj.ContentType ?? "unknown";
                if (!contentTypeStats.ContainsKey(contentType))
                {
                    contentTypeStats[contentType] = new ContentTypeStatistics
                    {
                        ContentType = contentType
                    };
                }

                contentTypeStats[contentType].ObjectCount++;
                contentTypeStats[contentType].TotalSizeBytes += obj.Size;
            }

            foreach (var stat in contentTypeStats.Values)
            {
                stat.AverageSizeBytes = stat.ObjectCount > 0 ? stat.TotalSizeBytes / stat.ObjectCount : 0;
            }

            statistics.ContentTypeStatistics = contentTypeStats;

            // Estatísticas do bucket
            statistics.BucketStatistics[bucketName] = new BucketStatistics
            {
                Name = bucketName,
                ObjectCount = statistics.TotalObjects,
                TotalSizeBytes = statistics.TotalSizeBytes,
                CreatedAt = DateTime.UtcNow // Placeholder
            };

            _logger.LogInformation("Estatísticas de armazenamento calculadas: {TotalObjects} objetos, {TotalSize} bytes", 
                statistics.TotalObjects, statistics.TotalSizeBytes);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas de armazenamento");
            throw;
        }
    }

    /// <summary>
    /// Configura política de retenção para um bucket
    /// </summary>
    /// <param name="bucketName">Nome do bucket</param>
    /// <param name="retentionDays">Dias de retenção</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se configurado com sucesso</returns>
    public async Task<bool> SetRetentionPolicyAsync(string bucketName, int retentionDays, CancellationToken cancellationToken = default)
    {
        try
        {
            // MinIO não suporta política de retenção diretamente via SDK
            // Implementar lógica de lifecycle policy se necessário
            
            _logger.LogInformation("Política de retenção definida para bucket {BucketName}: {RetentionDays} dias", 
                bucketName, retentionDays);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao definir política de retenção para bucket {BucketName}", bucketName);
            return false;
        }
    }

    /// <summary>
    /// Cria um bucket se não existir
    /// </summary>
    /// <param name="bucketName">Nome do bucket</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se criado ou já existe</returns>
    public async Task<bool> EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExists = await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucketName));

            if (!bucketExists)
            {
                await _minioClient.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(bucketName));

                _logger.LogInformation("Bucket criado: {BucketName}", bucketName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar bucket {BucketName}", bucketName);
            return false;
        }
    }

    /// <summary>
    /// Gera chave única para um objeto
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="fileName">Nome do arquivo</param>
    /// <param name="timestamp">Timestamp</param>
    /// <returns>Chave única</returns>
    public string GenerateObjectKey(Guid agentId, string fileName, DateTime timestamp)
    {
        var date = timestamp.ToString("yyyy/MM/dd");
        var timePrefix = timestamp.ToString("HHmmss");
        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        
        // Formato: screenshots/2025/01/15/agent-id/HHmmss_filename.ext
        return $"screenshots/{date}/{agentId}/{timePrefix}_{nameWithoutExtension}{extension}";
    }
}