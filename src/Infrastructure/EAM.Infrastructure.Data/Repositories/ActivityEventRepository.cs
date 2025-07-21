using Microsoft.EntityFrameworkCore;
using EAM.API.Core.Entities;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Data.Context;
using Npgsql;
using System.Data;

namespace EAM.Infrastructure.Data.Repositories;

/// <summary>
/// Implementação do repositório de eventos de atividade
/// </summary>
public class ActivityEventRepository : IActivityEventRepository
{
    private readonly EamDbContext _context;

    /// <summary>
    /// Construtor do repositório
    /// </summary>
    /// <param name="context">Contexto do banco de dados</param>
    public ActivityEventRepository(EamDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtém um evento por ID
    /// </summary>
    /// <param name="id">ID do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Evento encontrado ou null</returns>
    public async Task<ActivityEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ActivityEvents
            .AsNoTracking()
            .Include(e => e.Agent)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

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
    public async Task<IEnumerable<ActivityEvent>> GetByAgentAndPeriodAsync(
        Guid agentId,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _context.ActivityEvents
            .AsNoTracking()
            .Where(e => e.AgentId == agentId && e.Timestamp >= startDate && e.Timestamp <= endDate)
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

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
    public async Task<IEnumerable<ActivityEvent>> GetByUserAndPeriodAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _context.ActivityEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= startDate && e.Timestamp <= endDate)
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

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
    public async Task<IEnumerable<ActivityEvent>> GetByTypeAsync(
        string eventType,
        DateTime startDate,
        DateTime endDate,
        int pageSize = 100,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _context.ActivityEvents
            .AsNoTracking()
            .Where(e => e.EventType == eventType && e.Timestamp >= startDate && e.Timestamp <= endDate)
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Insere eventos em lote de forma eficiente usando Npgsql.Copy
    /// </summary>
    /// <param name="events">Lista de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de eventos inseridos</returns>
    public async Task<int> BulkInsertAsync(IEnumerable<ActivityEvent> events, CancellationToken cancellationToken = default)
    {
        var eventList = events.ToList();
        if (!eventList.Any()) return 0;

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var npgsqlConnection = (NpgsqlConnection)connection;
        
        using var writer = await npgsqlConnection.BeginBinaryImportAsync(
            "COPY activity_events (id, agent_id, user_id, timestamp, event_type, application_name, window_title, url, " +
            "process_name, process_id, duration_seconds, is_active, productivity_score, productivity_category, " +
            "screenshot_path, metadata, event_hash, agent_version, created_at, updated_at) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        var now = DateTime.UtcNow;
        
        foreach (var evt in eventList)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(evt.Id, cancellationToken);
            await writer.WriteAsync(evt.AgentId, cancellationToken);
            await writer.WriteAsync(evt.UserId, cancellationToken);
            await writer.WriteAsync(evt.Timestamp, cancellationToken);
            await writer.WriteAsync(evt.EventType, cancellationToken);
            await writer.WriteAsync(evt.ApplicationName, cancellationToken);
            await writer.WriteAsync(evt.WindowTitle, cancellationToken);
            await writer.WriteAsync(evt.Url, cancellationToken);
            await writer.WriteAsync(evt.ProcessName, cancellationToken);
            await writer.WriteAsync(evt.ProcessId, cancellationToken);
            await writer.WriteAsync(evt.DurationSeconds, cancellationToken);
            await writer.WriteAsync(evt.IsActive, cancellationToken);
            await writer.WriteAsync(evt.ProductivityScore, cancellationToken);
            await writer.WriteAsync(evt.ProductivityCategory, cancellationToken);
            await writer.WriteAsync(evt.ScreenshotPath, cancellationToken);
            await writer.WriteAsync(evt.Metadata, cancellationToken);
            await writer.WriteAsync(evt.EventHash, cancellationToken);
            await writer.WriteAsync(evt.AgentVersion, cancellationToken);
            await writer.WriteAsync(now, cancellationToken);
            await writer.WriteAsync(now, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
        return eventList.Count;
    }

    /// <summary>
    /// Cria um novo evento
    /// </summary>
    /// <param name="activityEvent">Dados do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Evento criado</returns>
    public async Task<ActivityEvent> CreateAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        _context.ActivityEvents.Add(activityEvent);
        await _context.SaveChangesAsync(cancellationToken);
        return activityEvent;
    }

    /// <summary>
    /// Atualiza um evento existente
    /// </summary>
    /// <param name="activityEvent">Dados do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Evento atualizado</returns>
    public async Task<ActivityEvent> UpdateAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        _context.ActivityEvents.Update(activityEvent);
        await _context.SaveChangesAsync(cancellationToken);
        return activityEvent;
    }

    /// <summary>
    /// Remove um evento
    /// </summary>
    /// <param name="id">ID do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.ActivityEvents
            .Where(e => e.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Obtém estatísticas de eventos por período
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas dos eventos</returns>
    public async Task<ActivityEventStatistics> GetStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ActivityEvents
            .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate);

        var totalEvents = await query.CountAsync(cancellationToken);
        var totalDays = (endDate - startDate).Days + 1;
        var averageEventsPerDay = totalDays > 0 ? (decimal)totalEvents / totalDays : 0;

        var averageProductivityScore = await query
            .Where(e => e.ProductivityScore.HasValue)
            .AverageAsync(e => (decimal?)e.ProductivityScore, cancellationToken);

        var totalActiveTime = await query
            .Where(e => e.DurationSeconds.HasValue)
            .SumAsync(e => (long?)e.DurationSeconds, cancellationToken) ?? 0;

        var uniqueApplications = await query
            .Where(e => !string.IsNullOrEmpty(e.ApplicationName))
            .Select(e => e.ApplicationName)
            .Distinct()
            .CountAsync(cancellationToken);

        var uniqueUrls = await query
            .Where(e => !string.IsNullOrEmpty(e.Url))
            .Select(e => e.Url)
            .Distinct()
            .CountAsync(cancellationToken);

        var eventTypeDistribution = await query
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventType, x => x.Count, cancellationToken);

        var productivityCategoryDistribution = await query
            .Where(e => !string.IsNullOrEmpty(e.ProductivityCategory))
            .GroupBy(e => e.ProductivityCategory)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category!, x => x.Count, cancellationToken);

        return new ActivityEventStatistics
        {
            TotalEvents = totalEvents,
            AverageEventsPerDay = averageEventsPerDay,
            AverageProductivityScore = averageProductivityScore,
            TotalActiveTimeSeconds = totalActiveTime,
            UniqueApplications = uniqueApplications,
            UniqueUrls = uniqueUrls,
            EventTypeDistribution = eventTypeDistribution,
            ProductivityCategoryDistribution = productivityCategoryDistribution
        };
    }

    /// <summary>
    /// Obtém eventos duplicados por hash
    /// </summary>
    /// <param name="eventHash">Hash do evento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de eventos duplicados</returns>
    public async Task<IEnumerable<ActivityEvent>> GetDuplicatesByHashAsync(
        string eventHash,
        CancellationToken cancellationToken = default)
    {
        return await _context.ActivityEvents
            .AsNoTracking()
            .Where(e => e.EventHash == eventHash)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Remove eventos antigos baseado em política de retenção
    /// </summary>
    /// <param name="cutoffDate">Data limite para remoção</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de eventos removidos</returns>
    public async Task<int> DeleteOldEventsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await _context.ActivityEvents
            .Where(e => e.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém aplicações mais usadas por período
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="topCount">Número de itens a retornar</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de aplicações mais usadas</returns>
    public async Task<IEnumerable<ApplicationUsage>> GetTopApplicationsAsync(
        DateTime startDate,
        DateTime endDate,
        int topCount = 10,
        CancellationToken cancellationToken = default)
    {
        var totalTime = await _context.ActivityEvents
            .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate && e.DurationSeconds.HasValue)
            .SumAsync(e => (long?)e.DurationSeconds, cancellationToken) ?? 0;

        return await _context.ActivityEvents
            .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate && !string.IsNullOrEmpty(e.ApplicationName))
            .GroupBy(e => e.ApplicationName)
            .Select(g => new ApplicationUsage
            {
                Name = g.Key!,
                TotalTimeSeconds = g.Sum(e => e.DurationSeconds) ?? 0,
                EventCount = g.Count(),
                AverageScore = g.Average(e => (decimal?)e.ProductivityScore),
                Percentage = totalTime > 0 ? (decimal)(g.Sum(e => e.DurationSeconds) ?? 0) / totalTime * 100 : 0
            })
            .OrderByDescending(x => x.TotalTimeSeconds)
            .Take(topCount)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém URLs mais visitadas por período
    /// </summary>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="topCount">Número de itens a retornar</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de URLs mais visitadas</returns>
    public async Task<IEnumerable<UrlUsage>> GetTopUrlsAsync(
        DateTime startDate,
        DateTime endDate,
        int topCount = 10,
        CancellationToken cancellationToken = default)
    {
        return await _context.ActivityEvents
            .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate && !string.IsNullOrEmpty(e.Url))
            .GroupBy(e => e.Url)
            .Select(g => new UrlUsage
            {
                Url = g.Key!,
                Domain = g.Key!.Contains("://") ? g.Key!.Split("://")[1].Split("/")[0] : g.Key!,
                Title = g.First().WindowTitle,
                TotalTimeSeconds = g.Sum(e => e.DurationSeconds) ?? 0,
                VisitCount = g.Count(),
                AverageScore = g.Average(e => (decimal?)e.ProductivityScore)
            })
            .OrderByDescending(x => x.TotalTimeSeconds)
            .Take(topCount)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém padrão de atividade por hora
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Padrão de atividade por hora</returns>
    public async Task<IEnumerable<HourlyActivity>> GetHourlyActivityPatternAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.ActivityEvents
            .Where(e => e.UserId == userId && e.Timestamp >= startDate && e.Timestamp <= endDate)
            .GroupBy(e => e.Timestamp.Hour)
            .Select(g => new HourlyActivity
            {
                Hour = g.Key,
                EventCount = g.Count(),
                AverageScore = g.Average(e => (decimal?)e.ProductivityScore),
                ActiveTimeSeconds = g.Sum(e => e.DurationSeconds) ?? 0
            })
            .OrderBy(x => x.Hour)
            .ToListAsync(cancellationToken);
    }
}