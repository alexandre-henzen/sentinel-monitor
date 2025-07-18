using EAM.Shared.DTOs;

namespace EAM.API.Services;

public interface IEventService
{
    Task<BatchProcessResult> CreateEventsBatchAsync(List<EventDto> events);
    Task<PagedResult<EventDto>> GetEventsAsync(Guid? agentId, DateTime? startDate, DateTime? endDate, string? eventType, int page, int pageSize);
    Task<EventStatistics> GetEventStatisticsAsync(Guid? agentId, DateTime? startDate, DateTime? endDate);
}

public class BatchProcessResult
{
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class EventStatistics
{
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public Dictionary<string, int> EventsByApplication { get; set; } = new();
    public Dictionary<DateOnly, int> EventsByDate { get; set; } = new();
    public double AverageProductivityScore { get; set; }
    public TimeSpan TotalActiveTime { get; set; }
}