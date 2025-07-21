namespace EAM.Infrastructure.Storage.Models;

/// <summary>
/// Estatísticas de armazenamento
/// </summary>
public class StorageStatistics
{
    /// <summary>
    /// Número total de objetos
    /// </summary>
    public int TotalObjects { get; set; }

    /// <summary>
    /// Tamanho total em bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Tamanho médio de objeto em bytes
    /// </summary>
    public long AverageObjectSizeBytes => TotalObjects > 0 ? TotalSizeBytes / TotalObjects : 0;

    /// <summary>
    /// Tamanho total formatado (KB, MB, GB)
    /// </summary>
    public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);

    /// <summary>
    /// Estatísticas por tipo de conteúdo
    /// </summary>
    public Dictionary<string, ContentTypeStatistics> ContentTypeStatistics { get; set; } = new();

    /// <summary>
    /// Estatísticas por bucket
    /// </summary>
    public Dictionary<string, BucketStatistics> BucketStatistics { get; set; } = new();

    /// <summary>
    /// Data da última atualização das estatísticas
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tempo gasto para calcular as estatísticas
    /// </summary>
    public TimeSpan CalculationTime { get; set; }

    /// <summary>
    /// Formatar bytes em formato legível
    /// </summary>
    /// <param name="bytes">Número de bytes</param>
    /// <returns>Tamanho formatado</returns>
    private static string FormatBytes(long bytes)
    {
        const long kilobyte = 1024;
        const long megabyte = kilobyte * 1024;
        const long gigabyte = megabyte * 1024;
        const long terabyte = gigabyte * 1024;

        return bytes switch
        {
            >= terabyte => $"{bytes / (double)terabyte:F2} TB",
            >= gigabyte => $"{bytes / (double)gigabyte:F2} GB",
            >= megabyte => $"{bytes / (double)megabyte:F2} MB",
            >= kilobyte => $"{bytes / (double)kilobyte:F2} KB",
            _ => $"{bytes} bytes"
        };
    }

    /// <summary>
    /// Obtém estatísticas resumidas
    /// </summary>
    /// <returns>Resumo das estatísticas</returns>
    public string GetSummary()
    {
        var topContentTypes = ContentTypeStatistics.Values
            .OrderByDescending(ct => ct.TotalSizeBytes)
            .Take(3)
            .ToList();

        var summary = $"Total: {TotalObjects} objetos ({TotalSizeFormatted})";
        
        if (topContentTypes.Any())
        {
            summary += $"\nTipos principais: {string.Join(", ", topContentTypes.Select(ct => $"{ct.ContentType} ({ct.ObjectCount})"))}";
        }

        return summary;
    }
}

/// <summary>
/// Estatísticas por tipo de conteúdo
/// </summary>
public class ContentTypeStatistics
{
    /// <summary>
    /// Tipo de conteúdo MIME
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Número de objetos deste tipo
    /// </summary>
    public int ObjectCount { get; set; }

    /// <summary>
    /// Tamanho total em bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Tamanho médio por objeto em bytes
    /// </summary>
    public long AverageSizeBytes { get; set; }

    /// <summary>
    /// Porcentagem do total de objetos
    /// </summary>
    public double PercentageOfTotal { get; set; }

    /// <summary>
    /// Tamanho total formatado
    /// </summary>
    public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);

    /// <summary>
    /// Tamanho médio formatado
    /// </summary>
    public string AverageSizeFormatted => FormatBytes(AverageSizeBytes);

    /// <summary>
    /// Formatar bytes em formato legível
    /// </summary>
    /// <param name="bytes">Número de bytes</param>
    /// <returns>Tamanho formatado</returns>
    private static string FormatBytes(long bytes)
    {
        const long kilobyte = 1024;
        const long megabyte = kilobyte * 1024;
        const long gigabyte = megabyte * 1024;

        return bytes switch
        {
            >= gigabyte => $"{bytes / (double)gigabyte:F2} GB",
            >= megabyte => $"{bytes / (double)megabyte:F2} MB",
            >= kilobyte => $"{bytes / (double)kilobyte:F2} KB",
            _ => $"{bytes} bytes"
        };
    }
}

/// <summary>
/// Estatísticas por bucket
/// </summary>
public class BucketStatistics
{
    /// <summary>
    /// Nome do bucket
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Número de objetos no bucket
    /// </summary>
    public int ObjectCount { get; set; }

    /// <summary>
    /// Tamanho total em bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Data de criação do bucket
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última modificação
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Tamanho médio por objeto em bytes
    /// </summary>
    public long AverageObjectSizeBytes => ObjectCount > 0 ? TotalSizeBytes / ObjectCount : 0;

    /// <summary>
    /// Tamanho total formatado
    /// </summary>
    public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);

    /// <summary>
    /// Formatar bytes em formato legível
    /// </summary>
    /// <param name="bytes">Número de bytes</param>
    /// <returns>Tamanho formatado</returns>
    private static string FormatBytes(long bytes)
    {
        const long kilobyte = 1024;
        const long megabyte = kilobyte * 1024;
        const long gigabyte = megabyte * 1024;

        return bytes switch
        {
            >= gigabyte => $"{bytes / (double)gigabyte:F2} GB",
            >= megabyte => $"{bytes / (double)megabyte:F2} MB",
            >= kilobyte => $"{bytes / (double)kilobyte:F2} KB",
            _ => $"{bytes} bytes"
        };
    }
}