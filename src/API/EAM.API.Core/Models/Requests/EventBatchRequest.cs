using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EAM.API.Core.Models.Requests;

/// <summary>
/// Requisição para submissão de lote de eventos do agente
/// </summary>
public class EventBatchRequest
{
    /// <summary>
    /// ID do agente que está enviando os eventos
    /// </summary>
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// Versão do agente
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp da coleta dos eventos
    /// </summary>
    [Required]
    public DateTime CollectionTimestamp { get; set; }

    /// <summary>
    /// Nome do computador
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// Nome do usuário
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Eventos serializados em formato NDJSON
    /// Cada linha representa um evento JSON
    /// </summary>
    [Required]
    public string EventsData { get; set; } = string.Empty;

    /// <summary>
    /// Quantidade de eventos no lote
    /// </summary>
    [Required]
    [Range(1, 10000)]
    public int EventCount { get; set; }

    /// <summary>
    /// Checksum MD5 dos dados para verificação de integridade
    /// </summary>
    [MaxLength(32)]
    public string? DataChecksum { get; set; }

    /// <summary>
    /// Indica se os dados estão comprimidos
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Metadados adicionais do lote
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Evento individual serializado para NDJSON
/// </summary>
public class EventData
{
    /// <summary>
    /// ID único do evento
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Timestamp do evento
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Tipo de evento
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Nome da aplicação
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Título da janela
    /// </summary>
    public string? WindowTitle { get; set; }

    /// <summary>
    /// URL (para navegadores)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Nome do processo
    /// </summary>
    public string? ProcessName { get; set; }

    /// <summary>
    /// ID do processo
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// Duração em segundos
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Pontuação de produtividade (0-100)
    /// </summary>
    public int? ProductivityScore { get; set; }

    /// <summary>
    /// Caminho do screenshot (se houver)
    /// </summary>
    public string? ScreenshotPath { get; set; }

    /// <summary>
    /// Metadados adicionais
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Metadata { get; set; }
}