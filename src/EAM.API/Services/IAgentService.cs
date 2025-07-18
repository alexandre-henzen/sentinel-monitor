using EAM.Shared.DTOs;
using EAM.Shared.Enums;
using EAM.API.Controllers;

namespace EAM.API.Services;

public interface IAgentService
{
    Task<AgentRegistrationResult> RegisterAgentAsync(AgentDto agentDto);
    Task UpdateHeartbeatAsync(HeartbeatDto heartbeatDto);
    Task<PagedResult<AgentDto>> GetAgentsAsync(int page, int pageSize);
    Task<AgentDto?> GetAgentAsync(Guid agentId);
    Task UpdateAgentStatusAsync(Guid agentId, string status);
    Task DeleteAgentAsync(Guid agentId);
}

public class AgentRegistrationResult
{
    public Guid AgentId { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = "Registered";
}