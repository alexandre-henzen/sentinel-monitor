using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAM.API.Core.Entities;

/// <summary>
/// Entidade representando scores diários agregados de produtividade
/// </summary>
[Table("scores_daily")]
public class DailyScore
{
    /// <summary>
    /// ID único do score diário
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// ID do agente
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
    /// Data do score (sem horário)
    /// </summary>
    [Column("score_date")]
    [Required]
    public DateOnly ScoreDate { get; set; }

    /// <summary>
    /// Score médio de produtividade do dia (0-100)
    /// </summary>
    [Column("average_productivity_score")]
    public decimal? AverageProductivityScore { get; set; }

    /// <summary>
    /// Score máximo atingido no dia
    /// </summary>
    [Column("max_productivity_score")]
    public int? MaxProductivityScore { get; set; }

    /// <summary>
    /// Score mínimo atingido no dia
    /// </summary>
    [Column("min_productivity_score")]
    public int? MinProductivityScore { get; set; }

    /// <summary>
    /// Total de eventos registrados no dia
    /// </summary>
    [Column("total_events")]
    public int TotalEvents { get; set; }

    /// <summary>
    /// Total de tempo ativo em segundos
    /// </summary>
    [Column("total_active_time_seconds")]
    public long TotalActiveTimeSeconds { get; set; }

    /// <summary>
    /// Total de tempo inativo em segundos
    /// </summary>
    [Column("total_idle_time_seconds")]
    public long TotalIdleTimeSeconds { get; set; }

    /// <summary>
    /// Número de aplicações únicas usadas
    /// </summary>
    [Column("unique_applications")]
    public int UniqueApplications { get; set; }

    /// <summary>
    /// Número de URLs únicas visitadas
    /// </summary>
    [Column("unique_urls")]
    public int UniqueUrls { get; set; }

    /// <summary>
    /// Número de screenshots capturados
    /// </summary>
    [Column("screenshots_count")]
    public int ScreenshotsCount { get; set; }

    /// <summary>
    /// Hora do primeiro evento do dia
    /// </summary>
    [Column("first_activity_time")]
    public TimeOnly? FirstActivityTime { get; set; }

    /// <summary>
    /// Hora do último evento do dia
    /// </summary>
    [Column("last_activity_time")]
    public TimeOnly? LastActivityTime { get; set; }

    /// <summary>
    /// Tempo total de trabalho efetivo (horas)
    /// </summary>
    [Column("effective_work_hours")]
    public decimal? EffectiveWorkHours { get; set; }

    /// <summary>
    /// Distribuição de tempo por categoria (JSON)
    /// </summary>
    [Column("time_distribution")]
    [Column(TypeName = "jsonb")]
    public string? TimeDistribution { get; set; }

    /// <summary>
    /// Top 5 aplicações mais usadas (JSON)
    /// </summary>
    [Column("top_applications")]
    [Column(TypeName = "jsonb")]
    public string? TopApplications { get; set; }

    /// <summary>
    /// Top 5 URLs mais visitadas (JSON)
    /// </summary>
    [Column("top_urls")]
    [Column(TypeName = "jsonb")]
    public string? TopUrls { get; set; }

    /// <summary>
    /// Estatísticas de produtividade por categoria (JSON)
    /// </summary>
    [Column("productivity_by_category")]
    [Column(TypeName = "jsonb")]
    public string? ProductivityByCategory { get; set; }

    /// <summary>
    /// Padrões de atividade por hora (JSON)
    /// </summary>
    [Column("hourly_activity_pattern")]
    [Column(TypeName = "jsonb")]
    public string? HourlyActivityPattern { get; set; }

    /// <summary>
    /// Informações de reuniões do Teams (JSON)
    /// </summary>
    [Column("teams_meetings_info")]
    [Column(TypeName = "jsonb")]
    public string? TeamsMeetingsInfo { get; set; }

    /// <summary>
    /// Métricas de qualidade dos dados
    /// </summary>
    [Column("data_quality_score")]
    public decimal? DataQualityScore { get; set; }

    /// <summary>
    /// Número de gaps nos dados (períodos sem eventos)
    /// </summary>
    [Column("data_gaps_count")]
    public int DataGapsCount { get; set; }

    /// <summary>
    /// Indica se os dados estão completos para o dia
    /// </summary>
    [Column("is_complete")]
    public bool IsComplete { get; set; }

    /// <summary>
    /// Observações ou notas sobre o dia
    /// </summary>
    [Column("notes")]
    [MaxLength(1024)]
    public string? Notes { get; set; }

    /// <summary>
    /// Tags associadas ao dia
    /// </summary>
    [Column("tags")]
    public string[]? Tags { get; set; }

    /// <summary>
    /// Timestamp de criação
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp de última atualização
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp do último cálculo/agregação
    /// </summary>
    [Column("last_calculated_at")]
    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Relacionamento com agente
    /// </summary>
    [ForeignKey(nameof(AgentId))]
    public virtual Agent Agent { get; set; } = null!;
}

/// <summary>
/// Classe para representar distribuição de tempo por categoria
/// </summary>
public class TimeDistributionItem
{
    /// <summary>
    /// Nome da categoria
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Tempo em segundos
    /// </summary>
    public long TimeSeconds { get; set; }

    /// <summary>
    /// Percentual do tempo total
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Score médio da categoria
    /// </summary>
    public decimal? AverageScore { get; set; }
}

/// <summary>
/// Classe para representar aplicação mais usada
/// </summary>
public class TopApplicationItem
{
    /// <summary>
    /// Nome da aplicação
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tempo total em segundos
    /// </summary>
    public long TotalTimeSeconds { get; set; }

    /// <summary>
    /// Número de eventos
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Score médio
    /// </summary>
    public decimal? AverageScore { get; set; }

    /// <summary>
    /// Percentual do tempo total
    /// </summary>
    public decimal Percentage { get; set; }
}

/// <summary>
/// Classe para representar URL mais visitada
/// </summary>
public class TopUrlItem
{
    /// <summary>
    /// URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Domínio
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Título da página
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Tempo total em segundos
    /// </summary>
    public long TotalTimeSeconds { get; set; }

    /// <summary>
    /// Número de visitas
    /// </summary>
    public int VisitCount { get; set; }

    /// <summary>
    /// Score médio
    /// </summary>
    public decimal? AverageScore { get; set; }
}

/// <summary>
/// Classe para representar padrão de atividade por hora
/// </summary>
public class HourlyActivityItem
{
    /// <summary>
    /// Hora do dia (0-23)
    /// </summary>
    public int Hour { get; set; }

    /// <summary>
    /// Número de eventos
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Score médio da hora
    /// </summary>
    public decimal? AverageScore { get; set; }

    /// <summary>
    /// Tempo ativo em segundos
    /// </summary>
    public long ActiveTimeSeconds { get; set; }

    /// <summary>
    /// Indica se foi um período de trabalho
    /// </summary>
    public bool IsWorkingHour { get; set; }
}