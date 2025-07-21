using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.Agent.Core.Models;

/// <summary>
/// Representa uma sessão de uso de uma aplicação pelo usuário
/// </summary>
public class SessionEvent
{
    /// <summary>
    /// Identificador único da sessão
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID do agente que coletou a sessão
    /// </summary>
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// ID do usuário
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Nome da aplicação (executável principal)
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Caminho completo da aplicação
    /// </summary>
    [MaxLength(512)]
    public string? ApplicationPath { get; set; }

    /// <summary>
    /// Título da janela ativa durante a sessão
    /// </summary>
    [MaxLength(512)]
    public string? WindowTitle { get; set; }

    /// <summary>
    /// URL se for navegador
    /// </summary>
    [MaxLength(2048)]
    public string? Url { get; set; }

    /// <summary>
    /// Domínio da URL se for navegador
    /// </summary>
    [MaxLength(256)]
    public string? Domain { get; set; }

    /// <summary>
    /// Tipo da sessão
    /// </summary>
    [Required]
    public SessionType SessionType { get; set; }

    /// <summary>
    /// Categoria de produtividade
    /// </summary>
    public ProductivityCategory Category { get; set; } = ProductivityCategory.Neutral;

    /// <summary>
    /// Score de produtividade (0-100)
    /// </summary>
    [Range(0, 100)]
    public int ProductivityScore { get; set; } = 50;

    /// <summary>
    /// Quando a sessão começou
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Quando a sessão terminou (null se ainda ativa)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duração da sessão em segundos (persistida no banco)
    /// </summary>
    [Required]
    public int DurationSeconds { get; set; } = 0;

    /// <summary>
    /// Se a sessão ainda está ativa
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Se foi sincronizado com a API
    /// </summary>
    [Required]
    public bool IsSynced { get; set; } = false;

    /// <summary>
    /// Quando foi criado o registro (timezone local)
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Última atualização (timezone local)
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Tipos de sessão
/// </summary>
public enum SessionType
{
    /// <summary>
    /// Aplicação desktop genérica
    /// </summary>
    Application = 1,

    /// <summary>
    /// Navegador web
    /// </summary>
    Browser = 2,

    /// <summary>
    /// Microsoft Teams
    /// </summary>
    Teams = 3,

    /// <summary>
    /// Editor de código/IDE
    /// </summary>
    Development = 4,

    /// <summary>
    /// Suite Office
    /// </summary>
    Office = 5,

    /// <summary>
    /// Aplicação de design/criatividade
    /// </summary>
    Design = 6,

    /// <summary>
    /// Jogos
    /// </summary>
    Gaming = 7,

    /// <summary>
    /// Comunicação (não Teams)
    /// </summary>
    Communication = 8,

    /// <summary>
    /// Sistema (Explorer, Settings, etc.)
    /// </summary>
    System = 9
}

/// <summary>
/// Categorias de produtividade
/// </summary>
public enum ProductivityCategory
{
    /// <summary>
    /// Altamente produtivo
    /// </summary>
    Productive = 1,

    /// <summary>
    /// Neutro/Indefinido
    /// </summary>
    Neutral = 2,

    /// <summary>
    /// Distração/Não produtivo
    /// </summary>
    Distraction = 3
}