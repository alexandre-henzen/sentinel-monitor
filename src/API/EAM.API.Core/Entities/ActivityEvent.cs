using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.API.Core.Entities;

/// <summary>
/// Entidade representando um evento de atividade do usuário
/// </summary>
[Table("activity_events")]
public class ActivityEvent
{
    /// <summary>
    /// ID único do evento
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// ID do agente que capturou o evento
    /// </summary>
    [Column("agent_id")]
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// ID do usuário
    /// </summary>
    [Column("user_id")]
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Timestamp do evento (particionado por data)
    /// </summary>
    [Column("timestamp")]
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Tipo de evento
    /// </summary>
    [Column("event_type")]
    [Required]
    [MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Nome da aplicação
    /// </summary>
    [Column("application_name")]
    [MaxLength(256)]
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Título da janela
    /// </summary>
    [Column("window_title")]
    [MaxLength(512)]
    public string? WindowTitle { get; set; }

    /// <summary>
    /// URL (para navegadores)
    /// </summary>
    [Column("url")]
    [MaxLength(2048)]
    public string? Url { get; set; }

    /// <summary>
    /// Nome do processo
    /// </summary>
    [Column("process_name")]
    [MaxLength(256)]
    public string? ProcessName { get; set; }

    /// <summary>
    /// ID do processo
    /// </summary>
    [Column("process_id")]
    public int? ProcessId { get; set; }

    /// <summary>
    /// Duração da atividade em segundos
    /// </summary>
    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Indica se a janela está ativa/em foco
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Pontuação de produtividade (0-100)
    /// </summary>
    [Column("productivity_score")]
    public int? ProductivityScore { get; set; }

    /// <summary>
    /// Categoria de produtividade
    /// </summary>
    [Column("productivity_category")]
    [MaxLength(64)]
    public string? ProductivityCategory { get; set; }

    /// <summary>
    /// Caminho do screenshot (se houver)
    /// </summary>
    [Column("screenshot_path")]
    [MaxLength(512)]
    public string? ScreenshotPath { get; set; }

    /// <summary>
    /// Metadados adicionais em formato JSON
    /// </summary>
    [Column("metadata")]
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }

    /// <summary>
    /// Hash do evento para deduplicação
    /// </summary>
    [Column("event_hash")]
    [MaxLength(64)]
    public string? EventHash { get; set; }

    /// <summary>
    /// Versão do agente que capturou o evento
    /// </summary>
    [Column("agent_version")]
    [MaxLength(32)]
    public string? AgentVersion { get; set; }

    /// <summary>
    /// Timestamp de criação no banco
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp de última atualização
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Relacionamento com agente
    /// </summary>
    [ForeignKey(nameof(AgentId))]
    public virtual Agent Agent { get; set; } = null!;

    /// <summary>
    /// Relacionamento com screenshot
    /// </summary>
    public virtual Screenshot? Screenshot { get; set; }
}

/// <summary>
/// Tipos de evento de atividade
/// </summary>
public static class ActivityEventTypes
{
    public const string WindowFocus = "window_focus";
    public const string WindowClose = "window_close";
    public const string WindowOpen = "window_open";
    public const string ApplicationStart = "application_start";
    public const string ApplicationEnd = "application_end";
    public const string BrowserNavigation = "browser_navigation";
    public const string BrowserUrl = "browser_url";
    public const string BrowserTitle = "browser_title";
    public const string TeamsCall = "teams_call";
    public const string TeamsCallEnd = "teams_call_end";
    public const string TeamsStatus = "teams_status";
    public const string TeamsChat = "teams_chat";
    public const string Screenshot = "screenshot";
    public const string ProcessStart = "process_start";
    public const string ProcessEnd = "process_end";
    public const string SystemIdle = "system_idle";
    public const string SystemActive = "system_active";
    public const string KeyboardActivity = "keyboard_activity";
    public const string MouseActivity = "mouse_activity";
    public const string CustomActivity = "custom_activity";
}

/// <summary>
/// Categorias de produtividade
/// </summary>
public static class ProductivityCategories
{
    public const string Productive = "productive";
    public const string Neutral = "neutral";
    public const string Unproductive = "unproductive";
    public const string Entertainment = "entertainment";
    public const string Communication = "communication";
    public const string Development = "development";
    public const string Meeting = "meeting";
    public const string Learning = "learning";
    public const string Administrative = "administrative";
    public const string Unknown = "unknown";
}