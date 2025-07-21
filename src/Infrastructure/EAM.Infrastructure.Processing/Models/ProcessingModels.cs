namespace EAM.Infrastructure.Processing.Models;

/// <summary>
/// Resultado do processamento de um lote de eventos
/// </summary>
public class EventBatchProcessingResult
{
    /// <summary>
    /// ID do lote processado
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// Indica se o processamento foi bem-sucedido
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Número de eventos processados com sucesso
    /// </summary>
    public int ProcessedEvents { get; set; }

    /// <summary>
    /// Número de eventos que falharam no processamento
    /// </summary>
    public int FailedEvents { get; set; }

    /// <summary>
    /// Tempo de processamento em milissegundos
    /// </summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// Mensagem de resultado
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Mensagem de erro (se houver)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Indica se o processamento é assíncrono
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// Timestamp do processamento
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Dados adicionais do resultado
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Resultado da validação de um lote
/// </summary>
public class BatchValidationResult
{
    /// <summary>
    /// Indica se o lote é válido
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Mensagem de erro da validação
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Lista de avisos da validação
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Detalhes da validação
    /// </summary>
    public Dictionary<string, object> ValidationDetails { get; set; } = new();
}

/// <summary>
/// Estatísticas de processamento
/// </summary>
public class ProcessingStatistics
{
    /// <summary>
    /// Capacidade total do canal
    /// </summary>
    public int ChannelCapacity { get; set; }

    /// <summary>
    /// Número atual de itens no canal
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Capacidade disponível no canal
    /// </summary>
    public int AvailableCapacity { get; set; }

    /// <summary>
    /// Número máximo de lotes concorrentes
    /// </summary>
    public int MaxConcurrentBatches { get; set; }

    /// <summary>
    /// Número atual de lotes sendo processados
    /// </summary>
    public int CurrentConcurrentBatches { get; set; }

    /// <summary>
    /// Total de lotes processados
    /// </summary>
    public long TotalBatchesProcessed { get; set; }

    /// <summary>
    /// Total de eventos processados
    /// </summary>
    public long TotalEventsProcessed { get; set; }

    /// <summary>
    /// Total de lotes com falha
    /// </summary>
    public long TotalFailedBatches { get; set; }

    /// <summary>
    /// Total de eventos com falha
    /// </summary>
    public long TotalFailedEvents { get; set; }

    /// <summary>
    /// Tempo médio de processamento por lote (ms)
    /// </summary>
    public double AverageProcessingTimeMs { get; set; }

    /// <summary>
    /// Taxa de processamento (lotes/segundo)
    /// </summary>
    public double ProcessingRate { get; set; }

    /// <summary>
    /// Taxa de sucesso (0.0 a 1.0)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Percentual de utilização do canal
    /// </summary>
    public double ChannelUtilization => ChannelCapacity > 0 ? (double)ChannelCount / ChannelCapacity : 0.0;

    /// <summary>
    /// Percentual de utilização de processamento concorrente
    /// </summary>
    public double ConcurrencyUtilization => MaxConcurrentBatches > 0 ? (double)CurrentConcurrentBatches / MaxConcurrentBatches : 0.0;

    /// <summary>
    /// Última atualização das estatísticas
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Status do processamento
    /// </summary>
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Healthy;

    /// <summary>
    /// Resumo das estatísticas
    /// </summary>
    public string Summary => $"Processados: {TotalBatchesProcessed} lotes, {TotalEventsProcessed} eventos. " +
                           $"Canal: {ChannelCount}/{ChannelCapacity} ({ChannelUtilization:P1}). " +
                           $"Taxa sucesso: {SuccessRate:P2}";
}

/// <summary>
/// Informações sobre o canal de processamento
/// </summary>
public class ChannelInfo
{
    /// <summary>
    /// Capacidade do canal
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Número atual de itens no canal
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Capacidade disponível
    /// </summary>
    public int AvailableCapacity { get; set; }

    /// <summary>
    /// Indica se o canal está completo
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Indica se o canal está cheio
    /// </summary>
    public bool IsFull { get; set; }

    /// <summary>
    /// Percentual de utilização
    /// </summary>
    public double UtilizationPercentage => Capacity > 0 ? (double)Count / Capacity : 0.0;

    /// <summary>
    /// Status do canal
    /// </summary>
    public ChannelStatus Status
    {
        get
        {
            if (IsCompleted) return ChannelStatus.Completed;
            if (IsFull) return ChannelStatus.Full;
            if (UtilizationPercentage > 0.8) return ChannelStatus.HighUtilization;
            if (UtilizationPercentage > 0.5) return ChannelStatus.MediumUtilization;
            return ChannelStatus.LowUtilization;
        }
    }
}

/// <summary>
/// Status do processamento
/// </summary>
public enum ProcessingStatus
{
    /// <summary>
    /// Processamento funcionando normalmente
    /// </summary>
    Healthy,

    /// <summary>
    /// Processamento com performance reduzida
    /// </summary>
    Degraded,

    /// <summary>
    /// Processamento com alertas
    /// </summary>
    Warning,

    /// <summary>
    /// Processamento em estado crítico
    /// </summary>
    Critical,

    /// <summary>
    /// Processamento parado
    /// </summary>
    Stopped
}

/// <summary>
/// Status do canal
/// </summary>
public enum ChannelStatus
{
    /// <summary>
    /// Canal com baixa utilização
    /// </summary>
    LowUtilization,

    /// <summary>
    /// Canal com média utilização
    /// </summary>
    MediumUtilization,

    /// <summary>
    /// Canal com alta utilização
    /// </summary>
    HighUtilization,

    /// <summary>
    /// Canal cheio
    /// </summary>
    Full,

    /// <summary>
    /// Canal completado
    /// </summary>
    Completed
}

/// <summary>
/// Evento de processamento para monitoramento
/// </summary>
public class ProcessingEvent
{
    /// <summary>
    /// ID único do evento
    /// </summary>
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID do lote relacionado
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// Tipo do evento
    /// </summary>
    public ProcessingEventType EventType { get; set; }

    /// <summary>
    /// Timestamp do evento
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Mensagem do evento
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Dados adicionais do evento
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Exceção relacionada (se houver)
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Tipo de evento de processamento
/// </summary>
public enum ProcessingEventType
{
    /// <summary>
    /// Lote recebido
    /// </summary>
    BatchReceived,

    /// <summary>
    /// Lote enfileirado
    /// </summary>
    BatchEnqueued,

    /// <summary>
    /// Início do processamento
    /// </summary>
    ProcessingStarted,

    /// <summary>
    /// Processamento concluído
    /// </summary>
    ProcessingCompleted,

    /// <summary>
    /// Falha no processamento
    /// </summary>
    ProcessingFailed,

    /// <summary>
    /// Tentativa de reprocessamento
    /// </summary>
    RetryAttempted,

    /// <summary>
    /// Lote descartado
    /// </summary>
    BatchDiscarded
}