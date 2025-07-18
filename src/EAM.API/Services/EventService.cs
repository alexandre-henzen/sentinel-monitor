using EAM.API.Data;
using EAM.Shared.DTOs;
using EAM.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EAM.API.Services;

public class EventService : IEventService
{
    private readonly EamDbContext _context;
    private readonly ILogger<EventService> _logger;

    public EventService(EamDbContext context, ILogger<EventService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BatchProcessResult> CreateEventsBatchAsync(List<EventDto> events)
    {
        var result = new BatchProcessResult
        {
            ProcessedCount = events.Count
        };

        try
        {
            var activityLogs = events.Select(e => new ActivityLog
            {
                AgentId = e.AgentId,
                EventType = e.EventType,
                ApplicationName = e.ApplicationName,
                WindowTitle = e.WindowTitle,
                Url = e.Url,
                ProcessName = e.ProcessName,
                ProcessId = e.ProcessId,
                DurationSeconds = e.DurationSeconds,
                ProductivityScore = e.ProductivityScore,
                EventTimestamp = e.EventTimestamp,
                ScreenshotPath = e.ScreenshotPath,
                Metadata = e.Metadata,
                CreatedAt = e.CreatedAt
            }).ToList();

            await _context.ActivityLogs.AddRangeAsync(activityLogs);
            await _context.SaveChangesAsync();

            result.SuccessCount = events.Count;
            _logger.LogInformation("Successfully processed {Count} events", events.Count);
        }
        catch (Exception ex)
        {
            result.ErrorCount = events.Count;
            result.Errors.Add($"Database error: {ex.Message}");
            _logger.LogError(ex, "Error processing event batch");
        }

        return result;
    }

    public async Task<PagedResult<EventDto>> GetEventsAsync(Guid? agentId, DateTime? startDate, DateTime? endDate, string? eventType, int page, int pageSize)
    {
        var query = _context.ActivityLogs.AsQueryable();

        if (agentId.HasValue)
            query = query.Where(e => e.AgentId == agentId.Value);

        if (startDate.HasValue)
            query = query.Where(e => e.EventTimestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.EventTimestamp <= endDate.Value);

        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(e => e.EventType == eventType);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.EventTimestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventDto
            {
                AgentId = e.AgentId,
                EventType = e.EventType,
                ApplicationName = e.ApplicationName,
                WindowTitle = e.WindowTitle,
                Url = e.Url,
                ProcessName = e.ProcessName,
                ProcessId = e.ProcessId,
                DurationSeconds = e.DurationSeconds,
                ProductivityScore = e.ProductivityScore,
                EventTimestamp = e.EventTimestamp,
                ScreenshotPath = e.ScreenshotPath,
                Metadata = e.Metadata,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<EventDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<EventStatistics> GetEventStatisticsAsync(Guid? agentId, DateTime? startDate, DateTime? endDate)
    {
        var query = _context.ActivityLogs.AsQueryable();

        if (agentId.HasValue)
            query = query.Where(e => e.AgentId == agentId.Value);

        if (startDate.HasValue)
            query = query.Where(e => e.EventTimestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.EventTimestamp <= endDate.Value);

        var events = await query.ToListAsync();

        var statistics = new EventStatistics
        {
            TotalEvents = events.Count,
            EventsByType = events.GroupBy(e => e.EventType).ToDictionary(g => g.Key, g => g.Count()),
            EventsByApplication = events.Where(e => !string.IsNullOrEmpty(e.ApplicationName))
                .GroupBy(e => e.ApplicationName!).ToDictionary(g => g.Key, g => g.Count()),
            EventsByDate = events.GroupBy(e => DateOnly.FromDateTime(e.EventTimestamp))
                .ToDictionary(g => g.Key, g => g.Count()),
            AverageProductivityScore = events.Where(e => e.ProductivityScore.HasValue)
                .Average(e => e.ProductivityScore ?? 0),
            TotalActiveTime = TimeSpan.FromSeconds(events.Sum(e => e.DurationSeconds ?? 0))
        };

        return statistics;
    }
}