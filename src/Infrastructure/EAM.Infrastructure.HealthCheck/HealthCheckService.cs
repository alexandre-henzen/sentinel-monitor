using EAM.API.Core.Interfaces;
using EAM.API.Core.Models.HealthCheck;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;
using Minio;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading.Channels;

namespace EAM.Infrastructure.HealthCheck;

/// <summary>
/// Serviço de health check para monitorar todos os componentes do sistema
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly HealthCheckSettings _settings;
    private readonly IConfiguration _configuration;
    private readonly Stopwatch _applicationStartTime;
    private static readonly Dictionary<string, HealthCheckResult> _cache = new();
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IOptions<HealthCheckSettings> settings,
        IConfiguration configuration)
    {
        _logger = logger;
        _settings = settings.Value;
        _configuration = configuration;
        _applicationStartTime = Stopwatch.StartNew();
    }

    /// <summary>
    /// Executa todos os health checks
    /// </summary>
    public async Task<HealthCheckSummary> GetHealthAsync()
    {
        var overallStopwatch = Stopwatch.StartNew();
        var results = new List<HealthCheckResult>();

        try
        {
            _logger.LogInformation("Iniciando health checks do sistema");

            // Executar todos os health checks em paralelo
            var healthCheckTasks = new List<Task<HealthCheckResult>>
            {
                CheckApplicationAsync(),
                CheckDatabaseAsync(),
                CheckRedisAsync(),
                CheckStorageAsync(),
                CheckEventProcessingAsync()
            };

            var healthCheckResults = await Task.WhenAll(healthCheckTasks);
            results.AddRange(healthCheckResults);

            overallStopwatch.Stop();

            // Determinar status geral
            var overallStatus = DetermineOverallStatus(results);

            var summary = new HealthCheckSummary
            {
                OverallStatus = overallStatus,
                TotalCheckTimeMs = overallStopwatch.ElapsedMilliseconds,
                CheckedAt = DateTime.UtcNow,
                Results = results,
                System = GetSystemInfo()
            };

            _logger.LogInformation("Health checks concluídos. Status geral: {Status}, Tempo total: {Time}ms", 
                overallStatus, overallStopwatch.ElapsedMilliseconds);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante execução dos health checks");
            
            return new HealthCheckSummary
            {
                OverallStatus = HealthStatus.Unhealthy,
                TotalCheckTimeMs = overallStopwatch.ElapsedMilliseconds,
                CheckedAt = DateTime.UtcNow,
                Results = results,
                System = GetSystemInfo()
            };
        }
    }

    /// <summary>
    /// Executa health check específico
    /// </summary>
    public async Task<HealthCheckResult> GetHealthAsync(string serviceName)
    {
        try
        {
            // Verificar cache se habilitado
            if (_settings.EnableCaching && await TryGetFromCacheAsync(serviceName, out var cachedResult))
            {
                return cachedResult;
            }

            var result = serviceName.ToLowerInvariant() switch
            {
                "application" => await CheckApplicationAsync(),
                "database" => await CheckDatabaseAsync(),
                "redis" => await CheckRedisAsync(),
                "storage" => await CheckStorageAsync(),
                "processing" => await CheckEventProcessingAsync(),
                _ => throw new ArgumentException($"Serviço desconhecido: {serviceName}")
            };

            // Armazenar no cache se habilitado
            if (_settings.EnableCaching)
            {
                await SetCacheAsync(serviceName, result);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante health check do serviço {ServiceName}", serviceName);
            
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                ServiceName = serviceName,
                Description = $"Erro durante verificação: {ex.Message}",
                Exception = ex.ToString(),
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Verifica saúde da aplicação
    /// </summary>
    public async Task<HealthCheckResult> CheckApplicationAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = new HealthCheckResult
            {
                ServiceName = "Application",
                Status = HealthStatus.Healthy,
                Description = "Aplicação executando normalmente",
                Timestamp = DateTime.UtcNow
            };

            // Verificar uso de memória
            var memoryUsed = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            result.Data["MemoryUsageMB"] = memoryUsed;

            // Verificar threads
            var threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
            result.Data["ThreadCount"] = threadCount;

            // Verificar uptime
            var uptime = _applicationStartTime.Elapsed;
            result.Data["UptimeSeconds"] = uptime.TotalSeconds;

            // Verificar se há warnings baseados em métricas
            if (memoryUsed > 500) // 500MB
            {
                result.Status = HealthStatus.Degraded;
                result.Description = "Alto uso de memória detectado";
            }

            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogDebug("Health check da aplicação concluído: {Status}", result.Status);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante health check da aplicação");
            
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                ServiceName = "Application",
                Description = $"Erro na verificação da aplicação: {ex.Message}",
                Exception = ex.ToString(),
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Verifica saúde do banco de dados
    /// </summary>
    public async Task<HealthCheckResult> CheckDatabaseAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new HealthCheckResult
                {
                    Status = HealthStatus.Unhealthy,
                    ServiceName = "Database",
                    Description = "String de conexão não configurada",
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Timestamp = DateTime.UtcNow
                };
            }

            using var connection = new NpgsqlConnection(connectionString);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            
            await connection.OpenAsync(timeoutCts.Token);
            
            // Executar query simples para verificar conectividade
            using var command = new NpgsqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(timeoutCts.Token);

            stopwatch.Stop();

            var healthResult = new HealthCheckResult
            {
                Status = HealthStatus.Healthy,
                ServiceName = "Database",
                Description = "Banco de dados PostgreSQL acessível",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };

            healthResult.Data["DatabaseType"] = "PostgreSQL";
            healthResult.Data["ConnectionState"] = connection.State.ToString();
            healthResult.Data["ServerVersion"] = connection.ServerVersion;

            _logger.LogDebug("Health check do banco de dados concluído: {Status}", healthResult.Status);
            return healthResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante health check do banco de dados");
            
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                ServiceName = "Database",
                Description = $"Erro na conexão com o banco de dados: {ex.Message}",
                Exception = ex.ToString(),
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Verifica saúde do Redis
    /// </summary>
    public async Task<HealthCheckResult> CheckRedisAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var connectionString = _configuration.GetConnectionString("RedisConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new HealthCheckResult
                {
                    Status = HealthStatus.Unhealthy,
                    ServiceName = "Redis",
                    Description = "String de conexão Redis não configurada",
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Timestamp = DateTime.UtcNow
                };
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            
            var redis = ConnectionMultiplexer.Connect(connectionString);
            var database = redis.GetDatabase();
            
            // Executar ping para verificar conectividade
            var ping = await database.PingAsync();
            
            // Testar operação básica
            var testKey = $"healthcheck:{Guid.NewGuid()}";
            await database.StringSetAsync(testKey, "test", TimeSpan.FromSeconds(10));
            var testValue = await database.StringGetAsync(testKey);
            await database.KeyDeleteAsync(testKey);

            stopwatch.Stop();

            var healthResult = new HealthCheckResult
            {
                Status = HealthStatus.Healthy,
                ServiceName = "Redis",
                Description = "Redis acessível e operacional",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };

            healthResult.Data["PingMs"] = ping.TotalMilliseconds;
            healthResult.Data["ReadWriteTest"] = testValue == "test" ? "Success" : "Failed";

            await redis.DisposeAsync();

            _logger.LogDebug("Health check do Redis concluído: {Status}", healthResult.Status);
            return healthResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante health check do Redis");
            
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                ServiceName = "Redis",
                Description = $"Erro na conexão com o Redis: {ex.Message}",
                Exception = ex.ToString(),
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Verifica saúde do storage (MinIO)
    /// </summary>
    public async Task<HealthCheckResult> CheckStorageAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var storageSettings = _configuration.GetSection("Storage");
            var endpoint = storageSettings["Endpoint"];
            var accessKey = storageSettings["AccessKey"];
            var secretKey = storageSettings["SecretKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                return new HealthCheckResult
                {
                    Status = HealthStatus.Unhealthy,
                    ServiceName = "Storage",
                    Description = "Configurações do MinIO não encontradas",
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Timestamp = DateTime.UtcNow
                };
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            
            var minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .Build();

            // Verificar se consegue listar buckets
            var buckets = await minioClient.ListBucketsAsync(timeoutCts.Token);

            stopwatch.Stop();

            var healthResult = new HealthCheckResult
            {
                Status = HealthStatus.Healthy,
                ServiceName = "Storage",
                Description = "MinIO acessível",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };

            healthResult.Data["Endpoint"] = endpoint;
            healthResult.Data["BucketCount"] = buckets.Buckets.Count;

            _logger.LogDebug("Health check do Storage concluído: {Status}", healthResult.Status);
            return healthResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante health check do Storage");
            
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                ServiceName = "Storage",
                Description = $"Erro na conexão com o MinIO: {ex.Message}",
                Exception = ex.ToString(),
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Verifica saúde do processamento de eventos
    /// </summary>
    public async Task<HealthCheckResult> CheckEventProcessingAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = new HealthCheckResult
            {
                ServiceName = "EventProcessing",
                Status = HealthStatus.Healthy,
                Description = "Processamento de eventos operacional",
                Timestamp = DateTime.UtcNow
            };

            // Verificar se as configurações de processamento estão presentes
            var processingSettings = _configuration.GetSection("Processing");
            var channelCapacity = processingSettings.GetValue<int>("ChannelCapacity");
            var maxConcurrentBatches = processingSettings.GetValue<int>("MaxConcurrentBatches");

            if (channelCapacity <= 0 || maxConcurrentBatches <= 0)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = "Configurações de processamento inválidas";
            }

            result.Data["ChannelCapacity"] = channelCapacity;
            result.Data["MaxConcurrentBatches"] = maxConcurrentBatches;
            result.Data["ProcessingEnabled"] = processingSettings.GetValue<bool>("EnableBackgroundProcessing");

            stopwatch.Stop();
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogDebug("Health check do processamento de eventos concluído: {Status}", result.Status);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante health check do processamento de eventos");
            
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                ServiceName = "EventProcessing",
                Description = $"Erro na verificação do processamento: {ex.Message}",
                Exception = ex.ToString(),
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Determina o status geral baseado nos resultados individuais
    /// </summary>
    private static HealthStatus DetermineOverallStatus(List<HealthCheckResult> results)
    {
        if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;
        
        if (results.Any(r => r.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;
        
        return HealthStatus.Healthy;
    }

    /// <summary>
    /// Obtém informações do sistema
    /// </summary>
    private SystemInfo GetSystemInfo()
    {
        return new SystemInfo
        {
            ServiceName = "EAM.API",
            Version = "5.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            MachineName = Environment.MachineName,
            Uptime = _applicationStartTime.Elapsed,
            MemoryUsageMB = GC.GetTotalMemory(false) / 1024 / 1024,
            CpuCount = Environment.ProcessorCount,
            DotNetVersion = Environment.Version.ToString()
        };
    }

    /// <summary>
    /// Tenta obter resultado do cache
    /// </summary>
    private async Task<bool> TryGetFromCacheAsync(string serviceName, out HealthCheckResult result)
    {
        result = null!;
        
        await _cacheLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(serviceName, out result))
            {
                var age = DateTime.UtcNow - result.Timestamp;
                if (age.TotalSeconds < _settings.CacheTimeoutSeconds)
                {
                    return true;
                }
                
                _cache.Remove(serviceName);
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        return false;
    }

    /// <summary>
    /// Armazena resultado no cache
    /// </summary>
    private async Task SetCacheAsync(string serviceName, HealthCheckResult result)
    {
        await _cacheLock.WaitAsync();
        try
        {
            _cache[serviceName] = result;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}