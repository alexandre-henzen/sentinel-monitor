using EAM.API.Core.Interfaces;
using EAM.API.Core.Models.HealthCheck;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EAM.Infrastructure.HealthCheck.Extensions;

/// <summary>
/// Extensões para configuração de health checks
/// </summary>
public static class HealthCheckServiceExtensions
{
    /// <summary>
    /// Adiciona serviços de health check
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar settings
        services.Configure<HealthCheckSettings>(configuration.GetSection(HealthCheckSettings.SectionName));
        
        // Registrar serviço customizado
        services.AddScoped<IHealthCheckService, HealthCheckService>();
        
        // Configurar health checks do ASP.NET Core
        var healthChecksBuilder = services.AddHealthChecks();
        
        // Adicionar health checks individuais
        AddDatabaseHealthCheck(healthChecksBuilder, configuration);
        AddRedisHealthCheck(healthChecksBuilder, configuration);
        AddStorageHealthCheck(healthChecksBuilder, configuration);
        AddApplicationHealthCheck(healthChecksBuilder);
        
        return services;
    }
    
    /// <summary>
    /// Adiciona health check do banco de dados
    /// </summary>
    private static void AddDatabaseHealthCheck(IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            builder.AddNpgSql(
                connectionString,
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "database", "postgresql" },
                timeout: TimeSpan.FromSeconds(30));
        }
    }
    
    /// <summary>
    /// Adiciona health check do Redis
    /// </summary>
    private static void AddRedisHealthCheck(IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RedisConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            builder.AddRedis(
                connectionString,
                name: "redis",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "cache", "redis" },
                timeout: TimeSpan.FromSeconds(30));
        }
    }
    
    /// <summary>
    /// Adiciona health check do storage (MinIO)
    /// </summary>
    private static void AddStorageHealthCheck(IHealthChecksBuilder builder, IConfiguration configuration)
    {
        var storageSettings = configuration.GetSection("Storage");
        var endpoint = storageSettings["Endpoint"];
        
        if (!string.IsNullOrEmpty(endpoint))
        {
            builder.AddCheck<MinIOHealthCheck>(
                name: "storage",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "storage", "minio" },
                timeout: TimeSpan.FromSeconds(30));
        }
    }
    
    /// <summary>
    /// Adiciona health check da aplicação
    /// </summary>
    private static void AddApplicationHealthCheck(IHealthChecksBuilder builder)
    {
        builder.AddCheck<ApplicationHealthCheck>(
            name: "application",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "application", "self" },
            timeout: TimeSpan.FromSeconds(10));
    }
}

/// <summary>
/// Health check customizado para MinIO
/// </summary>
public class MinIOHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MinIOHealthCheck> _logger;

    public MinIOHealthCheck(IConfiguration configuration, ILogger<MinIOHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var storageSettings = _configuration.GetSection("Storage");
            var endpoint = storageSettings["Endpoint"];
            var accessKey = storageSettings["AccessKey"];
            var secretKey = storageSettings["SecretKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                return HealthCheckResult.Unhealthy("Configurações do MinIO não encontradas");
            }

            var minioClient = new Minio.MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .Build();

            // Verificar se consegue listar buckets
            var buckets = await minioClient.ListBucketsAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["endpoint"] = endpoint,
                ["bucket_count"] = buckets.Buckets.Count,
                ["checked_at"] = DateTime.UtcNow
            };

            return HealthCheckResult.Healthy("MinIO acessível", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante health check do MinIO");
            return HealthCheckResult.Unhealthy($"Erro na conexão com o MinIO: {ex.Message}");
        }
    }
}

/// <summary>
/// Health check customizado para aplicação
/// </summary>
public class ApplicationHealthCheck : IHealthCheck
{
    private readonly ILogger<ApplicationHealthCheck> _logger;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public ApplicationHealthCheck(ILogger<ApplicationHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var memoryUsed = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            var threadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
            var uptime = DateTime.UtcNow - _startTime;

            var data = new Dictionary<string, object>
            {
                ["memory_usage_mb"] = memoryUsed,
                ["thread_count"] = threadCount,
                ["uptime_seconds"] = uptime.TotalSeconds,
                ["cpu_count"] = Environment.ProcessorCount,
                ["dotnet_version"] = Environment.Version.ToString(),
                ["machine_name"] = Environment.MachineName
            };

            var status = memoryUsed > 500 ? HealthStatus.Degraded : HealthStatus.Healthy;
            var description = status == HealthStatus.Degraded ? "Alto uso de memória" : "Aplicação saudável";

            return Task.FromResult(new HealthCheckResult(status, description, data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante health check da aplicação");
            return Task.FromResult(HealthCheckResult.Unhealthy($"Erro na verificação da aplicação: {ex.Message}"));
        }
    }
}