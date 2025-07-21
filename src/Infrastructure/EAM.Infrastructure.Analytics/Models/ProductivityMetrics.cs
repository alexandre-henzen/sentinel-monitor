namespace EAM.Infrastructure.Analytics.Models;

/// <summary>
/// Métricas de produtividade de um agente
/// </summary>
public class ProductivityMetrics
{
    /// <summary>
    /// ID do agente
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Data das métricas
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Tempo total ativo em minutos
    /// </summary>
    public int TotalActiveTimeMinutes { get; set; }

    /// <summary>
    /// Tempo total inativo em minutos
    /// </summary>
    public int TotalIdleTimeMinutes { get; set; }

    /// <summary>
    /// Número total de eventos de teclado
    /// </summary>
    public int TotalKeyboardEvents { get; set; }

    /// <summary>
    /// Número total de cliques do mouse
    /// </summary>
    public int TotalMouseClicks { get; set; }

    /// <summary>
    /// Número total de movimentos do mouse
    /// </summary>
    public int TotalMouseMovements { get; set; }

    /// <summary>
    /// Número total de mudanças de aplicação
    /// </summary>
    public int TotalApplicationSwitches { get; set; }

    /// <summary>
    /// Número total de screenshots capturadas
    /// </summary>
    public int TotalScreenshots { get; set; }

    /// <summary>
    /// Score de produtividade (0-100)
    /// </summary>
    public decimal ProductivityScore { get; set; }

    /// <summary>
    /// Score de atividade (0-100)
    /// </summary>
    public decimal ActivityScore { get; set; }

    /// <summary>
    /// Score de foco (0-100)
    /// </summary>
    public decimal FocusScore { get; set; }

    /// <summary>
    /// Aplicação mais utilizada
    /// </summary>
    public string? TopApplication { get; set; }

    /// <summary>
    /// Tempo gasto na aplicação mais utilizada (minutos)
    /// </summary>
    public int TopApplicationTimeMinutes { get; set; }

    /// <summary>
    /// Horário de início da atividade
    /// </summary>
    public TimeSpan? StartTime { get; set; }

    /// <summary>
    /// Horário de fim da atividade
    /// </summary>
    public TimeSpan? EndTime { get; set; }

    /// <summary>
    /// Número de breaks/pausas
    /// </summary>
    public int BreakCount { get; set; }

    /// <summary>
    /// Tempo total em breaks (minutos)
    /// </summary>
    public int TotalBreakTimeMinutes { get; set; }

    /// <summary>
    /// Métricas por hora
    /// </summary>
    public List<HourlyMetrics> HourlyMetrics { get; set; } = new();

    /// <summary>
    /// Métricas por aplicação
    /// </summary>
    public List<ApplicationMetrics> ApplicationMetrics { get; set; } = new();

    /// <summary>
    /// Timestamp da última atualização
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Indica se as métricas foram finalizadas para o dia
    /// </summary>
    public bool IsFinalized { get; set; }

    /// <summary>
    /// Percentual de tempo ativo
    /// </summary>
    public decimal ActiveTimePercentage => 
        TotalActiveTimeMinutes + TotalIdleTimeMinutes > 0 
            ? (decimal)TotalActiveTimeMinutes / (TotalActiveTimeMinutes + TotalIdleTimeMinutes) * 100 
            : 0;

    /// <summary>
    /// Tempo total trabalhado em horas
    /// </summary>
    public decimal TotalWorkedHours => (TotalActiveTimeMinutes + TotalIdleTimeMinutes) / 60m;

    /// <summary>
    /// Eventos por minuto (KPM - Key Presses per Minute)
    /// </summary>
    public decimal EventsPerMinute => 
        TotalActiveTimeMinutes > 0 
            ? (decimal)(TotalKeyboardEvents + TotalMouseClicks) / TotalActiveTimeMinutes 
            : 0;
}

/// <summary>
/// Métricas por hora
/// </summary>
public class HourlyMetrics
{
    /// <summary>
    /// Hora (0-23)
    /// </summary>
    public int Hour { get; set; }

    /// <summary>
    /// Minutos ativos na hora
    /// </summary>
    public int ActiveMinutes { get; set; }

    /// <summary>
    /// Minutos inativos na hora
    /// </summary>
    public int IdleMinutes { get; set; }

    /// <summary>
    /// Eventos de teclado na hora
    /// </summary>
    public int KeyboardEvents { get; set; }

    /// <summary>
    /// Cliques do mouse na hora
    /// </summary>
    public int MouseClicks { get; set; }

    /// <summary>
    /// Score de produtividade da hora
    /// </summary>
    public decimal ProductivityScore { get; set; }
}

/// <summary>
/// Métricas por aplicação
/// </summary>
public class ApplicationMetrics
{
    /// <summary>
    /// Nome da aplicação
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Tempo total gasto na aplicação (minutos)
    /// </summary>
    public int TotalTimeMinutes { get; set; }

    /// <summary>
    /// Número de vezes que a aplicação foi focada
    /// </summary>
    public int FocusCount { get; set; }

    /// <summary>
    /// Eventos de teclado na aplicação
    /// </summary>
    public int KeyboardEvents { get; set; }

    /// <summary>
    /// Cliques do mouse na aplicação
    /// </summary>
    public int MouseClicks { get; set; }

    /// <summary>
    /// Percentual do tempo total
    /// </summary>
    public decimal TimePercentage { get; set; }

    /// <summary>
    /// Categoria da aplicação
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Indica se é uma aplicação produtiva
    /// </summary>
    public bool IsProductive { get; set; }

    /// <summary>
    /// Janelas/títulos mais utilizados
    /// </summary>
    public List<string> TopWindows { get; set; } = new();
}

/// <summary>
/// Métricas agregadas por período
/// </summary>
public class AggregatedMetrics
{
    /// <summary>
    /// ID do agente
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Tipo de período (Daily, Weekly, Monthly)
    /// </summary>
    public PeriodType PeriodType { get; set; }

    /// <summary>
    /// Data de início do período
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Data de fim do período
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Tempo total ativo em minutos
    /// </summary>
    public int TotalActiveTimeMinutes { get; set; }

    /// <summary>
    /// Tempo total inativo em minutos
    /// </summary>
    public int TotalIdleTimeMinutes { get; set; }

    /// <summary>
    /// Score médio de produtividade
    /// </summary>
    public decimal AverageProductivityScore { get; set; }

    /// <summary>
    /// Score médio de atividade
    /// </summary>
    public decimal AverageActivityScore { get; set; }

    /// <summary>
    /// Score médio de foco
    /// </summary>
    public decimal AverageFocusScore { get; set; }

    /// <summary>
    /// Número total de dias trabalhados
    /// </summary>
    public int WorkingDays { get; set; }

    /// <summary>
    /// Aplicações mais utilizadas
    /// </summary>
    public List<ApplicationMetrics> TopApplications { get; set; } = new();

    /// <summary>
    /// Tendência do período (Improving, Stable, Declining)
    /// </summary>
    public TrendType Trend { get; set; }

    /// <summary>
    /// Insights e observações
    /// </summary>
    public List<string> Insights { get; set; } = new();
}

/// <summary>
/// Tipo de período para agregação
/// </summary>
public enum PeriodType
{
    /// <summary>
    /// Métricas diárias
    /// </summary>
    Daily,

    /// <summary>
    /// Métricas semanais
    /// </summary>
    Weekly,

    /// <summary>
    /// Métricas mensais
    /// </summary>
    Monthly,

    /// <summary>
    /// Métricas trimestrais
    /// </summary>
    Quarterly,

    /// <summary>
    /// Métricas anuais
    /// </summary>
    Yearly
}

/// <summary>
/// Tipo de tendência
/// </summary>
public enum TrendType
{
    /// <summary>
    /// Melhorando
    /// </summary>
    Improving,

    /// <summary>
    /// Estável
    /// </summary>
    Stable,

    /// <summary>
    /// Declinando
    /// </summary>
    Declining,

    /// <summary>
    /// Dados insuficientes
    /// </summary>
    Insufficient
}

/// <summary>
/// Comparação de métricas
/// </summary>
public class MetricsComparison
{
    /// <summary>
    /// Período atual
    /// </summary>
    public AggregatedMetrics CurrentPeriod { get; set; } = new();

    /// <summary>
    /// Período anterior
    /// </summary>
    public AggregatedMetrics PreviousPeriod { get; set; } = new();

    /// <summary>
    /// Variação percentual na produtividade
    /// </summary>
    public decimal ProductivityChange { get; set; }

    /// <summary>
    /// Variação percentual na atividade
    /// </summary>
    public decimal ActivityChange { get; set; }

    /// <summary>
    /// Variação percentual no foco
    /// </summary>
    public decimal FocusChange { get; set; }

    /// <summary>
    /// Variação no tempo ativo (minutos)
    /// </summary>
    public int ActiveTimeChange { get; set; }

    /// <summary>
    /// Resumo das principais mudanças
    /// </summary>
    public List<string> KeyChanges { get; set; } = new();

    /// <summary>
    /// Indica se houve melhora geral
    /// </summary>
    public bool IsImprovement { get; set; }
}