using EAM.Shared.DTOs;
using EAM.Shared.Constants;
using EAM.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace EAM.API.Controllers;

[ApiController]
[Route(ApiEndpoints.Events)]
[Authorize(Policy = "Agent")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly ILogger<EventsController> _logger;

    public EventsController(IEventService eventService, ILogger<EventsController> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    [HttpPost("batch")]
    public async Task<IActionResult> CreateEventBatch([FromBody] List<EventDto> events)
    {
        try
        {
            if (!events.Any())
            {
                return BadRequest("No events provided");
            }

            var result = await _eventService.CreateEventsBatchAsync(events);
            
            _logger.LogInformation("Processed {Count} events in batch", events.Count);
            
            return Ok(new { 
                ProcessedCount = result.ProcessedCount,
                SuccessCount = result.SuccessCount,
                ErrorCount = result.ErrorCount,
                Errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event batch");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("ndjson")]
    [Consumes("application/x-ndjson")]
    public async Task<IActionResult> CreateEventsNdjson()
    {
        try
        {
            var events = new List<EventDto>();
            
            using var reader = new StreamReader(Request.Body);
            string? line;
            
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                try
                {
                    var eventDto = JsonSerializer.Deserialize<EventDto>(line);
                    if (eventDto != null)
                    {
                        events.Add(eventDto);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid JSON in NDJSON line: {Line}", line);
                }
            }

            if (!events.Any())
            {
                return BadRequest("No valid events found in NDJSON");
            }

            var result = await _eventService.CreateEventsBatchAsync(events);
            
            _logger.LogInformation("Processed {Count} events from NDJSON", events.Count);
            
            return Ok(new { 
                ProcessedCount = result.ProcessedCount,
                SuccessCount = result.SuccessCount,
                ErrorCount = result.ErrorCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing NDJSON events");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetEvents(
        [FromQuery] Guid? agentId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? eventType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            var result = await _eventService.GetEventsAsync(
                agentId, startDate, endDate, eventType, page, pageSize);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetEventStatistics(
        [FromQuery] Guid? agentId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            var statistics = await _eventService.GetEventStatisticsAsync(agentId, startDate, endDate);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving event statistics");
            return StatusCode(500, "Internal server error");
        }
    }
}