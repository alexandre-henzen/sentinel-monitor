using EAM.API.Core.Entities;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para repositório de eventos de atividade
/// </summary>
public interface IActivityEventRepository
{
    /// <summary>
    /// Obtém um evento por ID
    /// </summary>
    /// <param name="id">ID do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Evento encontrado ou null</returns>
    Task<ActivityEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém eventos por agente e período
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de eventos</returns>
    Task<IEnumerable<ActivityEvent>> GetByAgentAndPeriodAsync(
        Guid agentId,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém eventos por usuário e período
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de eventos</returns>
    Task<IEnumerable<ActivityEvent>> GetByUserAndPeriodAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém eventos por tipo
    /// </summary>
    /// <param name="eventType">Tipo do evento</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="pageSize">Tamanho da página</param>
    /// <param name="pageNumber">Número da página</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de eventos</returns>
    Task<IEnumerable<ActivityEvent>> GetByTypeAsync(
        string eventType,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insere eventos em lote de forma eficiente
    /// </summary>
    /// <param name="events">Lista de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de eventos inseridos</returns>
    Task<int> BulkInsertAsync(IEnumerable<ActivityEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um novo evento
    /// </summary>
    /// <param name="activityEvent">Dados do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Evento criado</returns>
    Task<ActivityEvent> CreateAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza um evento existente
    /// </summary>
    /// <param name="activityEvent">Dados do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Evento atualizado</returns>
    Task<ActivityEvent> UpdateAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um evento
    /// </summary>
    /// <param name="id">ID do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas de eventos por período
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas dos eventos</returns>
    Task<ActivityEventStatistics> GetStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém eventos duplicados por hash
    /// </summary>
    /// <param name="eventHash">Hash do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de eventos duplicados</returns>
    Task<IEnumerable<ActivityEvent>> GetDuplicatesByHashAsync(
        string eventHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove eventos antigos baseado em política de retenção
    /// </summary>
    /// <param name="cutoffDate">Data limite para remoção</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de eventos removidos</returns>
    Task<int> DeleteOldEventsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém aplicações mais usadas por período
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="topCount">Número de itens a retornar</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de aplicações mais usadas</returns>
    Task<IEnumerable<ApplicationUsage>> GetTopApplicationsAsync(
        DateTime startDate,
        DateTime endDate,
        int topCount = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém URLs mais visitadas por período
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="topCount">Número de itens a retornar</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de URLs mais visitadas</returns>
    Task<IEnumerable<UrlUsage>> GetTopUrlsAsync(
        DateTime startDate,
        DateTime endDate,
        int topCount = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém padrão de atividade por hora
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Padrão de atividade por hora</returns>
    Task<IEnumerable<HourlyActivity>> GetHourlyActivityPatternAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Estatísticas de eventos de atividade
/// </summary>
public class ActivityEventStatistics
{
    /// <summary>
    /// Total de eventos
    /// </summary>
    public long TotalEvents { get; set; }

    /// <summary>
    /// Eventos por dia
    /// </summary>
    public decimal AverageEventsPerDay { get; set; }

    /// <summary>
    /// Score médio de produtividade
    /// </summary>
    public decimal? AverageProductivityScore { get; set; }

    /// <summary>
    /// Tempo total ativo (segundos)
    /// </summary>
    public long TotalActiveTimeSeconds { get; set; }

    /// <summary>
    /// Aplicações únicas
    /// </summary>
    public int UniqueApplications { get; set; }

    /// <summary>
    /// URLs únicas
    /// </summary>
    public int UniqueUrls { get; set; }

    /// <summary>
    /// Distribuição por tipo de evento
    /// </summary>
    public Dictionary<string, int> EventTypeDistribution { get; set; } = new();

    /// <summary>
    /// Distribuição por categoria de produtividade
    /// </summary>
    public Dictionary<string, int> ProductivityCategoryDistribution { get; set; } = new();
}

/// <summary>
/// Uso de aplicação
/// </summary>
public class ApplicationUsage
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
/// Uso de URL
/// </summary>
public class UrlUsage
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
/// Atividade por hora
/// </summary>
public class HourlyActivity
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
    /// Score médio
    /// </summary>
    public decimal? AverageScore { get; set; }

    /// <summary>
    /// Tempo ativo em segundos
    /// </summary>
    public long ActiveTimeSeconds { get; set; }
}