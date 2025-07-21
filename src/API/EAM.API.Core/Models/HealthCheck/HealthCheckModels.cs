namespace EAM.API.Core.Models.HealthCheck;

/// <summary>
/// Resultado do health check
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Status do health check
    /// </summary>
    public HealthStatus Status { get; set; }
    
    /// <summary>
    /// Nome do serviço verificado
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Descrição do status
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Dados adicionais do health check
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
    
    /// <summary>
    /// Tempo de resposta em millisegundos
    /// </summary>
    public long ResponseTimeMs { get; set; }
    
    /// <summary>
    /// Timestamp da verificação
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Exceção se houver erro
    /// </summary>
    public string? Exception { get; set; }
}

/// <summary>
/// Status do health check
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Serviço saudável
    /// </summary>
    Healthy,
    
    /// <summary>
    /// Serviço degradado
    /// </summary>
    Degraded,
    
    /// <summary>
    /// Serviço indisponível
    /// </summary>
    Unhealthy
}

/// <summary>
/// Resumo geral do health check
/// </summary>
public class HealthCheckSummary
{
    /// <summary>
    /// Status geral do sistema
    /// </summary>
    public HealthStatus OverallStatus { get; set; }
    
    /// <summary>
    /// Tempo total de verificação
    /// </summary>
    public long TotalCheckTimeMs { get; set; }
    
    /// <summary>
    /// Timestamp da verificação
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Resultados individuais dos health checks
    /// </summary>
    public List<HealthCheckResult> Results { get; set; } = new();
    
    /// <summary>
    /// Informações do sistema
    /// </summary>
    public SystemInfo System { get; set; } = new();
}

/// <summary>
/// Informações do sistema
/// </summary>
public class SystemInfo
{
    /// <summary>
    /// Nome do serviço
    /// </summary>
    public string ServiceName { get; set; } = "EAM.API";
    
    /// <summary>
    /// Versão do serviço
    /// </summary>
    public string Version { get; set; } = "5.0.0";
    
    /// <summary>
    /// Ambiente de execução
    /// </summary>
    public string Environment { get; set; } = string.Empty;
    
    /// <summary>
    /// Nome da máquina
    /// </summary>
    public string MachineName { get; set; } = Environment.MachineName;
    
    /// <summary>
    /// Tempo de atividade
    /// </summary>
    public TimeSpan Uptime { get; set; }
    
    /// <summary>
    /// Uso de memória em MB
    /// </summary>
    public long MemoryUsageMB { get; set; }
    
    /// <summary>
    /// Número de CPUs
    /// </summary>
    public int CpuCount { get; set; } = Environment.ProcessorCount;
    
    /// <summary>
    /// Versão do .NET
    /// </summary>
    public string DotNetVersion { get; set; } = Environment.Version.ToString();
}

/// <summary>
/// Configurações de health check
/// </summary>
public class HealthCheckSettings
{
    /// <summary>
    /// Nome da seção no appsettings.json
    /// </summary>
    public const string SectionName = "HealthCheck";
    
    /// <summary>
    /// Timeout para health checks em segundos
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Habilitar health checks detalhados
    /// </summary>
    public bool EnableDetailedChecks { get; set; } = true;
    
    /// <summary>
    /// Habilitar cache dos resultados
    /// </summary>
    public bool EnableCaching { get; set; } = true;
    
    /// <summary>
    /// Tempo de cache em segundos
    /// </summary>
    public int CacheTimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Habilitar métricas de health check
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
    
    /// <summary>
    /// Habilitar logs de health check
    /// </summary>
    public bool EnableLogging { get; set; } = true;
    
    /// <summary>
    /// Intervalo de verificação em segundos (para background checks)
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 60;
}