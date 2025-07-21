using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.Agent.Core.Models;

/// <summary>
/// Evento base de atividade do usuário
/// </summary>
[Table("ActivityEvents")]
public class ActivityEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID do agente que capturou o evento
    /// </summary>
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// ID do usuário (obtido do sistema)
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Timestamp do evento
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Tipo de evento de atividade
    /// </summary>
    [Required]
    public ActivityType Type { get; set; }

    /// <summary>
    /// Nome da aplicação/processo
    /// </summary>
    [MaxLength(256)]
    public string? Application { get; set; }

    /// <summary>
    /// Título da janela
    /// </summary>
    [MaxLength(512)]
    public string? WindowTitle { get; set; }

    /// <summary>
    /// URL (para navegadores)
    /// </summary>
    [MaxLength(2048)]
    public string? Url { get; set; }

    /// <summary>
    /// Duração da atividade
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Indica se a janela está ativa/em foco
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Pontuação de produtividade (0-100)
    /// </summary>
    public int? ProductivityScore { get; set; }

    /// <summary>
    /// Metadados adicionais em formato JSON
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Indica se já foi sincronizado com a API
    /// </summary>
    public bool IsSynced { get; set; }

    /// <summary>
    /// Data de criação do registro
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data da última modificação
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tipos de evento de atividade
/// </summary>
public enum ActivityType
{
    WindowFocus = 1,
    ApplicationStart = 2,
    ApplicationEnd = 3,
    BrowserNavigation = 4,
    TeamsCall = 5,
    TeamsCallEnd = 6,
    Screenshot = 7,
    ProcessStart = 8,
    ProcessEnd = 9,
    SystemIdle = 10,
    SystemActive = 11
}