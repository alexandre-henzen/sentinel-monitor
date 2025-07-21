using EAM.API.Core.Models.Requests;

namespace EAM.Infrastructure.Processing.Models;

/// <summary>
/// Lote de eventos para processamento assíncrono
/// </summary>
public class EventBatch
{
    /// <summary>
    /// ID único do lote
    /// </summary>
    public Guid BatchId { get; set; }

    /// <summary>
    /// ID do agente que enviou os eventos
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Lista de eventos no lote
    /// </summary>
    public List<EventDataRequest> Events { get; set; } = new();

    /// <summary>
    /// Timestamp de quando o lote foi recebido
    /// </summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>
    /// Prioridade do lote para processamento
    /// </summary>
    public BatchPriority Priority { get; set; }

    /// <summary>
    /// Origem do lote (API, Queue, etc.)
    /// </summary>
    public string Source { get; set; } = "API";

    /// <summary>
    /// Número de tentativas de processamento
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Número máximo de tentativas
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Timestamp da última tentativa de processamento
    /// </summary>
    public DateTime? LastProcessingAttempt { get; set; }

    /// <summary>
    /// Mensagem de erro da última tentativa (se houver)
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Dados adicionais do lote
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Indica se o lote pode ser reprocessado
    /// </summary>
    public bool CanRetry => RetryCount < MaxRetries;

    /// <summary>
    /// Tamanho do lote em eventos
    /// </summary>
    public int Size => Events?.Count ?? 0;

    /// <summary>
    /// Idade do lote desde que foi recebido
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - ReceivedAt;
}

/// <summary>
/// Prioridade do lote para processamento
/// </summary>
public enum BatchPriority
{
    /// <summary>
    /// Prioridade baixa - processamento em segundo plano
    /// </summary>
    Low = 1,

    /// <summary>
    /// Prioridade média - processamento normal
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Prioridade alta - processamento prioritário
    /// </summary>
    High = 3,

    /// <summary>
    /// Prioridade crítica - processamento imediato
    /// </summary>
    Critical = 4
}