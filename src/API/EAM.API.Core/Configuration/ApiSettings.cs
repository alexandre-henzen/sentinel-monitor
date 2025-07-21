namespace EAM.API.Core.Configuration;

/// <summary>
/// Configurações da API
/// </summary>
public class ApiSettings
{
    /// <summary>
    /// Configurações de paginação
    /// </summary>
    public PaginationSettings Pagination { get; set; } = new();

    /// <summary>
    /// Configurações de rate limiting
    /// </summary>
    public RateLimitingSettings RateLimiting { get; set; } = new();

    /// <summary>
    /// Configurações de ingestão de dados
    /// </summary>
    public DataIngestionSettings DataIngestion { get; set; } = new();

    /// <summary>
    /// Configurações de storage
    /// </summary>
    public StorageSettings Storage { get; set; } = new();

    /// <summary>
    /// Configurações de cache
    /// </summary>
    public CacheSettings Cache { get; set; } = new();

    /// <summary>
    /// Configurações de autenticação
    /// </summary>
    public AuthenticationSettings Authentication { get; set; } = new();

    /// <summary>
    /// Configurações de health checks
    /// </summary>
    public HealthCheckSettings HealthChecks { get; set; } = new();

    /// <summary>
    /// Configurações de telemetria
    /// </summary>
    public TelemetrySettings Telemetry { get; set; } = new();
}

/// <summary>
/// Configurações de paginação
/// </summary>
public class PaginationSettings
{
    /// <summary>
    /// Tamanho padrão da página
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Tamanho máximo da página
    /// </summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// Número máximo de páginas
    /// </summary>
    public int MaxPageNumber { get; set; } = 1000;
}

/// <summary>
/// Configurações de rate limiting
/// </summary>
public class RateLimitingSettings
{
    /// <summary>
    /// Habilitar rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Requisições por minuto por IP
    /// </summary>
    public int RequestsPerMinutePerIp { get; set; } = 100;

    /// <summary>
    /// Requisições por minuto por agente
    /// </summary>
    public int RequestsPerMinutePerAgent { get; set; } = 200;

    /// <summary>
    /// Tamanho máximo da requisição em bytes
    /// </summary>
    public long MaxRequestSizeBytes { get; set; } = 10_000_000; // 10MB

    /// <summary>
    /// Timeout da requisição em segundos
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Configurações de ingestão de dados
/// </summary>
public class DataIngestionSettings
{
    /// <summary>
    /// Tamanho máximo do lote de eventos
    /// </summary>
    public int MaxBatchSize { get; set; } = 10000;

    /// <summary>
    /// Timeout de processamento do lote em segundos
    /// </summary>
    public int BatchProcessingTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Número máximo de workers para processamento
    /// </summary>
    public int MaxWorkers { get; set; } = 10;

    /// <summary>
    /// Tamanho da fila de processamento
    /// </summary>
    public int QueueSize { get; set; } = 1000;

    /// <summary>
    /// Intervalo de retry em segundos
    /// </summary>
    public int RetryIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Número máximo de tentativas
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Habilitar compressão de dados
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Habilitar validação de checksum
    /// </summary>
    public bool EnableChecksumValidation { get; set; } = true;
}

/// <summary>
/// Configurações de storage
/// </summary>
public class StorageSettings
{
    /// <summary>
    /// Configurações do MinIO
    /// </summary>
    public MinIOSettings MinIO { get; set; } = new();

    /// <summary>
    /// Bucket padrão para screenshots
    /// </summary>
    public string DefaultBucket { get; set; } = "eam-screenshots";

    /// <summary>
    /// TTL padrão para objetos em dias
    /// </summary>
    public int DefaultTtlDays { get; set; } = 90;

    /// <summary>
    /// Tamanho máximo de upload em bytes
    /// </summary>
    public long MaxUploadSizeBytes { get; set; } = 50_000_000; // 50MB

    /// <summary>
    /// Tempo de expiração da URL em minutos
    /// </summary>
    public int UrlExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Habilitar versionamento de objetos
    /// </summary>
    public bool EnableVersioning { get; set; } = true;
}

/// <summary>
/// Configurações do MinIO
/// </summary>
public class MinIOSettings
{
    /// <summary>
    /// Endpoint do MinIO
    /// </summary>
    public string Endpoint { get; set; } = "localhost:9000";

    /// <summary>
    /// Chave de acesso
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Chave secreta
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Usar SSL
    /// </summary>
    public bool UseSSL { get; set; } = false;

    /// <summary>
    /// Região
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Timeout de conexão em segundos
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout de operação em segundos
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// Configurações de cache
/// </summary>
public class CacheSettings
{
    /// <summary>
    /// Configurações do Redis
    /// </summary>
    public RedisSettings Redis { get; set; } = new();

    /// <summary>
    /// Tempo de expiração padrão em minutos
    /// </summary>
    public int DefaultExpiryMinutes { get; set; } = 30;

    /// <summary>
    /// Tempo de expiração deslizante em minutos
    /// </summary>
    public int SlidingExpiryMinutes { get; set; } = 15;

    /// <summary>
    /// Prefixo das chaves
    /// </summary>
    public string KeyPrefix { get; set; } = "eam:api:";

    /// <summary>
    /// Habilitar cache distribuído
    /// </summary>
    public bool EnableDistributedCache { get; set; } = true;
}

/// <summary>
/// Configurações do Redis
/// </summary>
public class RedisSettings
{
    /// <summary>
    /// String de conexão
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Banco de dados
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Timeout de conexão em segundos
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout de operação em segundos
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Número de tentativas de retry
    /// </summary>
    public int RetryCount { get; set; } = 3;
}

/// <summary>
/// Configurações de autenticação
/// </summary>
public class AuthenticationSettings
{
    /// <summary>
    /// Configurações do JWT
    /// </summary>
    public JwtSettings Jwt { get; set; } = new();

    /// <summary>
    /// Habilitar autenticação por certificado
    /// </summary>
    public bool EnableCertificateAuth { get; set; } = true;

    /// <summary>
    /// Habilitar verificação de revogação
    /// </summary>
    public bool EnableRevocationCheck { get; set; } = true;

    /// <summary>
    /// Timeout de autenticação em segundos
    /// </summary>
    public int AuthenticationTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Configurações do JWT
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Chave secreta
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Emissor
    /// </summary>
    public string Issuer { get; set; } = "EAM.API";

    /// <summary>
    /// Audiência
    /// </summary>
    public string Audience { get; set; } = "EAM.Clients";

    /// <summary>
    /// Tempo de expiração em minutos
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Tolerância de relógio
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Configurações de health checks
/// </summary>
public class HealthCheckSettings
{
    /// <summary>
    /// Habilitar health checks
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Intervalo de verificação em segundos
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout de verificação em segundos
    /// </summary>
    public int CheckTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Número de falhas antes de marcar como não saudável
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Verificações habilitadas
    /// </summary>
    public string[] EnabledChecks { get; set; } = { "database", "cache", "storage" };
}

/// <summary>
/// Configurações de telemetria
/// </summary>
public class TelemetrySettings
{
    /// <summary>
    /// Habilitar telemetria
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Habilitar métricas
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Habilitar tracing
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Habilitar logging estruturado
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Endpoint do Jaeger
    /// </summary>
    public string? JaegerEndpoint { get; set; }

    /// <summary>
    /// Endpoint do Prometheus
    /// </summary>
    public string? PrometheusEndpoint { get; set; }

    /// <summary>
    /// Taxa de amostragem para tracing
    /// </summary>
    public double TracingSampleRate { get; set; } = 1.0;

    /// <summary>
    /// Intervalo de exportação em segundos
    /// </summary>
    public int ExportIntervalSeconds { get; set; } = 30;
}