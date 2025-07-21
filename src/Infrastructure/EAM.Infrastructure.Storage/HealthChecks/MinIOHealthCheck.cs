using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using EAM.API.Core.Configuration;

namespace EAM.Infrastructure.Storage.HealthChecks;

/// <summary>
/// Health check para MinIO
/// </summary>
public class MinIOHealthCheck : IHealthCheck
{
    private readonly IMinioClient _minioClient;
    private readonly StorageSettings _storageSettings;
    private readonly ILogger<MinIOHealthCheck> _logger;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="minioClient">Cliente MinIO</param>
    /// <param name="storageSettings">Configurações de storage</param>
    /// <param name="logger">Logger</param>
    public MinIOHealthCheck(
        IMinioClient minioClient,
        IOptions<StorageSettings> storageSettings,
        ILogger<MinIOHealthCheck> logger)
    {
        _minioClient = minioClient;
        _storageSettings = storageSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executa o health check
    /// </summary>
    /// <param name="context">Contexto do health check</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado do health check</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Verificar se conseguimos listar buckets (operação básica)
            var buckets = await _minioClient.ListBucketsAsync();
            
            var responseTime = DateTime.UtcNow - startTime;
            
            // Verificar se o bucket padrão existe
            var defaultBucketExists = buckets.Buckets.Any(b => b.Name == _storageSettings.DefaultBucket);
            
            var data = new Dictionary<string, object>
            {
                ["endpoint"] = _storageSettings.Endpoint,
                ["default_bucket"] = _storageSettings.DefaultBucket,
                ["default_bucket_exists"] = defaultBucketExists,
                ["bucket_count"] = buckets.Buckets.Count,
                ["response_time_ms"] = responseTime.TotalMilliseconds,
                ["check_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };

            if (!defaultBucketExists)
            {
                _logger.LogWarning("MinIO health check: bucket padrão {DefaultBucket} não encontrado", 
                    _storageSettings.DefaultBucket);
                
                return HealthCheckResult.Degraded(
                    $"MinIO está funcionando mas o bucket padrão '{_storageSettings.DefaultBucket}' não existe", 
                    data: data);
            }

            // Verificar se conseguimos fazer operações básicas no bucket padrão
            try
            {
                var testObjectKey = $"health-check-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                var testContent = System.Text.Encoding.UTF8.GetBytes("health-check-test");
                
                // Tentar fazer upload de um objeto de teste
                using var stream = new MemoryStream(testContent);
                await _minioClient.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(_storageSettings.DefaultBucket)
                    .WithObject(testObjectKey)
                    .WithStreamData(stream)
                    .WithObjectSize(testContent.Length)
                    .WithContentType("text/plain"));

                // Verificar se o objeto existe
                await _minioClient.StatObjectAsync(new StatObjectArgs()
                    .WithBucket(_storageSettings.DefaultBucket)
                    .WithObject(testObjectKey));

                // Remover o objeto de teste
                await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(_storageSettings.DefaultBucket)
                    .WithObject(testObjectKey));

                data["write_test"] = "success";
                data["read_test"] = "success";
                data["delete_test"] = "success";
                
                _logger.LogDebug("MinIO health check passou em todos os testes");
                
                return HealthCheckResult.Healthy(
                    $"MinIO está funcionando corretamente (resposta em {responseTime.TotalMilliseconds:F2}ms)",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MinIO health check: falha nos testes de operação");
                
                data["write_test"] = "failed";
                data["error"] = ex.Message;
                
                return HealthCheckResult.Degraded(
                    $"MinIO está acessível mas falhou nos testes de operação: {ex.Message}",
                    data: data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MinIO health check falhou");
            
            var errorData = new Dictionary<string, object>
            {
                ["endpoint"] = _storageSettings.Endpoint,
                ["error"] = ex.Message,
                ["error_type"] = ex.GetType().Name,
                ["check_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };

            return HealthCheckResult.Unhealthy(
                $"Falha ao conectar com MinIO: {ex.Message}",
                exception: ex,
                data: errorData);
        }
    }
}