namespace EAM.Infrastructure.Cache.Models;

/// <summary>
/// Estatísticas do cache Redis
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Número total de chaves no cache
    /// </summary>
    public int TotalKeys { get; set; }

    /// <summary>
    /// Memória utilizada em bytes
    /// </summary>
    public long UsedMemoryBytes { get; set; }

    /// <summary>
    /// Memória utilizada formatada (KB, MB, GB)
    /// </summary>
    public string UsedMemoryFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Memória total do sistema em bytes
    /// </summary>
    public long TotalSystemMemoryBytes { get; set; }

    /// <summary>
    /// Memória máxima configurada em bytes
    /// </summary>
    public long MaxMemoryBytes { get; set; }

    /// <summary>
    /// Taxa de hit do cache (0.0 a 1.0)
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// Taxa de hit formatada como porcentagem
    /// </summary>
    public string HitRateFormatted => $"{HitRate:P2}";

    /// <summary>
    /// Porcentagem de memória utilizada
    /// </summary>
    public double MemoryUsagePercentage => MaxMemoryBytes > 0 ? (double)UsedMemoryBytes / MaxMemoryBytes : 0.0;

    /// <summary>
    /// Porcentagem de memória utilizada formatada
    /// </summary>
    public string MemoryUsagePercentageFormatted => $"{MemoryUsagePercentage:P2}";

    /// <summary>
    /// Data da última atualização das estatísticas
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indica se o cache está saudável
    /// </summary>
    public bool IsHealthy => HitRate >= 0.7 && MemoryUsagePercentage < 0.9;

    /// <summary>
    /// Status do cache baseado nas métricas
    /// </summary>
    public CacheStatus Status
    {
        get
        {
            if (MemoryUsagePercentage >= 0.95)
                return CacheStatus.Critical;
            
            if (MemoryUsagePercentage >= 0.85 || HitRate < 0.5)
                return CacheStatus.Warning;
            
            if (HitRate >= 0.7)
                return CacheStatus.Healthy;
            
            return CacheStatus.Degraded;
        }
    }

    /// <summary>
    /// Resumo das estatísticas
    /// </summary>
    public string Summary => $"Chaves: {TotalKeys}, Memória: {UsedMemoryFormatted}, Hit Rate: {HitRateFormatted}, Status: {Status}";
}

/// <summary>
/// Status do cache
/// </summary>
public enum CacheStatus
{
    /// <summary>
    /// Cache funcionando perfeitamente
    /// </summary>
    Healthy,

    /// <summary>
    /// Cache funcionando com performance reduzida
    /// </summary>
    Degraded,

    /// <summary>
    /// Cache com alertas de performance ou uso de memória
    /// </summary>
    Warning,

    /// <summary>
    /// Cache em estado crítico
    /// </summary>
    Critical
}