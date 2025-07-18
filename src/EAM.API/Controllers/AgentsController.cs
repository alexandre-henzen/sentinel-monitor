using EAM.Shared.DTOs;
using EAM.Shared.Constants;
using EAM.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EAM.API.Controllers;

[ApiController]
[Route(ApiEndpoints.Agents)]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAgent([FromBody] AgentDto agentDto)
    {
        try
        {
            var result = await _agentService.RegisterAgentAsync(agentDto);
            
            _logger.LogInformation("Agent registered: {MachineId}", agentDto.MachineId);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering agent");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("heartbeat")]
    [Authorize(Policy = "Agent")]
    public async Task<IActionResult> SendHeartbeat([FromBody] HeartbeatDto heartbeatDto)
    {
        try
        {
            await _agentService.UpdateHeartbeatAsync(heartbeatDto);
            
            _logger.LogDebug("Heartbeat received from agent: {AgentId}", heartbeatDto.AgentId);
            
            return Ok(new { Status = "OK", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> GetAgents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _agentService.GetAgentsAsync(page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{agentId}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> GetAgent(Guid agentId)
    {
        try
        {
            var agent = await _agentService.GetAgentAsync(agentId);
            
            if (agent == null)
            {
                return NotFound();
            }
            
            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent {AgentId}", agentId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{agentId}/status")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> UpdateAgentStatus(Guid agentId, [FromBody] UpdateAgentStatusDto statusDto)
    {
        try
        {
            await _agentService.UpdateAgentStatusAsync(agentId, statusDto.Status);
            
            _logger.LogInformation("Agent status updated: {AgentId} -> {Status}", agentId, statusDto.Status);
            
            return Ok(new { Status = "Updated", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent status");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{agentId}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteAgent(Guid agentId)
    {
        try
        {
            await _agentService.DeleteAgentAsync(agentId);
            
            _logger.LogInformation("Agent deleted: {AgentId}", agentId);
            
            return Ok(new { Status = "Deleted", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting agent");
            return StatusCode(500, "Internal server error");
        }
    }
}

public class HeartbeatDto
{
    public Guid AgentId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "Active";
    public string Version { get; set; } = string.Empty;
}

public class UpdateAgentStatusDto
{
    public string Status { get; set; } = string.Empty;
}