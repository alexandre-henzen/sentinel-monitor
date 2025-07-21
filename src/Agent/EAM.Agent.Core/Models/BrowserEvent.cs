using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.Agent.Core.Models;

/// <summary>
/// Evento específico de navegação no browser
/// </summary>
[Table("BrowserEvents")]
public class BrowserEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID do evento de atividade relacionado
    /// </summary>
    [Required]
    public Guid ActivityEventId { get; set; }

    /// <summary>
    /// Tipo de navegador
    /// </summary>
    [Required]
    public BrowserType BrowserType { get; set; }

    /// <summary>
    /// URL completa da página
    /// </summary>
    [MaxLength(2048)]
    public string? Url { get; set; }

    /// <summary>
    /// Domínio da página
    /// </summary>
    [MaxLength(256)]
    public string? Domain { get; set; }

    /// <summary>
    /// Título da página
    /// </summary>
    [MaxLength(512)]
    public string? PageTitle { get; set; }

    /// <summary>
    /// Tipo de evento de navegação
    /// </summary>
    public NavigationType NavigationType { get; set; }

    /// <summary>
    /// URL anterior (para navegação)
    /// </summary>
    [MaxLength(2048)]
    public string? PreviousUrl { get; set; }

    /// <summary>
    /// Tempo gasto na página (em segundos)
    /// </summary>
    public int TimeOnPage { get; set; }

    /// <summary>
    /// Número de abas abertas
    /// </summary>
    public int TabCount { get; set; }

    /// <summary>
    /// Indica se a página está em foco
    /// </summary>
    public bool IsActiveTab { get; set; }

    /// <summary>
    /// Indica se está no modo incógnito/privado
    /// </summary>
    public bool IsIncognito { get; set; }

    /// <summary>
    /// Timestamp do evento
    /// </summary>
    [Required]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Relacionamento com ActivityEvent
    /// </summary>
    [ForeignKey(nameof(ActivityEventId))]
    public ActivityEvent ActivityEvent { get; set; } = null!;
}

/// <summary>
/// Tipos de navegador suportados
/// </summary>
public enum BrowserType
{
    Chrome = 1,
    Edge = 2,
    Firefox = 3,
    Safari = 4,
    Opera = 5,
    Internet_Explorer = 6,
    Other = 99
}

/// <summary>
/// Tipos de navegação
/// </summary>
public enum NavigationType
{
    PageLoad = 1,
    Navigation = 2,
    Refresh = 3,
    BackForward = 4,
    NewTab = 5,
    CloseTab = 6,
    FocusChange = 7
}