using System.ComponentModel.DataAnnotations;

namespace EAM.API.Core.Models.Responses;

/// <summary>
/// Resposta do health check da API
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// Status geral da API
    /// </summary>
    [Required]
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Timestamp da verificação
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tempo total da verificação em milissegundos
    /// </summary>
    [Required]
    public long TotalCheckTimeMs { get; set; }

    /// <summary>
    /// Versão da API
    /// </summary>
    [Required]
    public string Version { get; set; } = "5.0.0";

    /// <summary>
    /// Ambiente (Development, Production)
    /// </summary>
    [Required]
    public string Environment { get; set; } = "Production";

    /// <summary>
    /// Tempo de atividade da API
    /// </summary>
    [Required]
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Verificações de componentes individuais
    /// </summary>
    [Required]
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();

    /// <summary>
    /// Métricas da API
    /// </summary>
    public ApiMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Informações do sistema
    /// </summary>
    public SystemHealth System { get; set; } = new();

    /// <summary>
    /// Dependências externas
    /// </summary>
    public Dictionary<string, DependencyHealth> Dependencies { get; set; } = new();

    /// <summary>
    /// Avisos não críticos
    /// </summary>
    public string[] Warnings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Erros críticos
    /// </summary>
    public string[] Errors { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Próxima verificação agendada
    /// </summary>
    public DateTime NextCheck { get; set; }

    /// <summary>
    /// Informações adicionais
    /// </summary>
    public Dictionary<string, object>? AdditionalInfo { get; set; }
}

/// <summary>
/// Status de saúde
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Saudável - todos os componentes funcionando
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Degradado - alguns componentes com problemas
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Não saudável - componentes críticos falhando
    /// </summary>
    Unhealthy = 2,

    /// <summary>
    /// Desconhecido - não foi possível verificar
    /// </summary>
    Unknown = 3
}

/// <summary>
/// Saúde de componente individual
/// </summary>
public class ComponentHealth
{
    /// <summary>
    /// Status do componente
    /// </summary>
    [Required]
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Descrição do status
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tempo de verificação em milissegundos
    /// </summary>
    public long CheckTimeMs { get; set; }

    /// <summary>
    /// Timestamp da última verificação
    /// </summary>
    public DateTime LastCheck { get; set; }

    /// <summary>
    /// Dados específicos do componente
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// Exceção ocorrida (se houver)
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Tags do componente
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Métricas da API
/// </summary>
public class ApiMetrics
{
    /// <summary>
    /// Total de requisições processadas
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Requisições por segundo (média)
    /// </summary>
    public double RequestsPerSecond { get; set; }

    /// <summary>
    /// Tempo médio de resposta em milissegundos
    /// </summary>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>
    /// Taxa de erro (percentual)
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Agentes conectados atualmente
    /// </summary>
    public int ActiveAgents { get; set; }

    /// <summary>
    /// Eventos processados na última hora
    /// </summary>
    public long EventsProcessedLastHour { get; set; }

    /// <summary>
    /// Screenshots processados na última hora
    /// </summary>
    public long ScreenshotsProcessedLastHour { get; set; }

    /// <summary>
    /// Uso de memória em bytes
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Uso de CPU (percentual)
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Conexões ativas com o banco de dados
    /// </summary>
    public int ActiveDatabaseConnections { get; set; }

    /// <summary>
    /// Itens em cache
    /// </summary>
    public long CacheItemCount { get; set; }

    /// <summary>
    /// Taxa de acerto do cache (percentual)
    /// </summary>
    public double CacheHitRate { get; set; }
}

/// <summary>
/// Saúde do sistema
/// </summary>
public class SystemHealth
{
    /// <summary>
    /// Memória total disponível em bytes
    /// </summary>
    public long TotalMemoryBytes { get; set; }

    /// <summary>
    /// Memória em uso em bytes
    /// </summary>
    public long UsedMemoryBytes { get; set; }

    /// <summary>
    /// Espaço em disco total em bytes
    /// </summary>
    public long TotalDiskSpaceBytes { get; set; }

    /// <summary>
    /// Espaço em disco usado em bytes
    /// </summary>
    public long UsedDiskSpaceBytes { get; set; }

    /// <summary>
    /// Número de processadores
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// Tempo de atividade do sistema
    /// </summary>
    public TimeSpan SystemUptime { get; set; }

    /// <summary>
    /// Carga do sistema (load average)
    /// </summary>
    public double[] LoadAverage { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Temperatura do sistema (se disponível)
    /// </summary>
    public double? SystemTemperature { get; set; }

    /// <summary>
    /// Informações de rede
    /// </summary>
    public NetworkInfo Network { get; set; } = new();
}

/// <summary>
/// Informações de rede
/// </summary>
public class NetworkInfo
{
    /// <summary>
    /// Bytes enviados
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// Bytes recebidos
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// Conexões TCP ativas
    /// </summary>
    public int ActiveTcpConnections { get; set; }

    /// <summary>
    /// Latência média em milissegundos
    /// </summary>
    public double AverageLatencyMs { get; set; }
}

/// <summary>
/// Saúde de dependência externa
/// </summary>
public class DependencyHealth
{
    /// <summary>
    /// Nome da dependência
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Status da dependência
    /// </summary>
    [Required]
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Descrição do status
    /// </summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tempo de resposta em milissegundos
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Timestamp da última verificação
    /// </summary>
    public DateTime LastCheck { get; set; }

    /// <summary>
    /// Versão da dependência
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Endpoint da dependência
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Dados específicos da dependência
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }
}