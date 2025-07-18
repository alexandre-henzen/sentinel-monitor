using EAM.API.Data;
using EAM.Shared.DTOs;
using EAM.Shared.Models.Entities;
using EAM.Shared.Enums;
using EAM.API.Controllers;
using Microsoft.EntityFrameworkCore;

namespace EAM.API.Services;

public class AgentService : IAgentService
{
    private readonly EamDbContext _context;
    private readonly IAuthService _authService;
    private readonly ILogger<AgentService> _logger;

    public AgentService(EamDbContext context, IAuthService authService, ILogger<AgentService> logger)
    {
        _context = context;
        _authService = authService;
        _logger = logger;
    }

    public async Task<AgentRegistrationResult> RegisterAgentAsync(AgentDto agentDto)
    {
        var existingAgent = await _context.Agents
            .FirstOrDefaultAsync(a => a.MachineId == agentDto.MachineId);

        Agent agent;
        if (existingAgent != null)
        {
            // Update existing agent
            existingAgent.MachineName = agentDto.MachineName;
            existingAgent.UserName = agentDto.UserName;
            existingAgent.OsVersion = agentDto.OsVersion;
            existingAgent.AgentVersion = agentDto.AgentVersion;
            existingAgent.LastSeen = DateTime.UtcNow;
            existingAgent.Status = AgentStatus.Active;
            existingAgent.UpdatedAt = DateTime.UtcNow;
            agent = existingAgent;
        }
        else
        {
            // Create new agent
            agent = new Agent
            {
                MachineId = agentDto.MachineId,
                MachineName = agentDto.MachineName,
                UserName = agentDto.UserName,
                OsVersion = agentDto.OsVersion,
                AgentVersion = agentDto.AgentVersion,
                Status = AgentStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            _context.Agents.Add(agent);
        }

        await _context.SaveChangesAsync();

        var token = await _authService.GenerateTokenAsync(agent.MachineId, "eam.agent");
        
        return new AgentRegistrationResult
        {
            AgentId = agent.Id,
            AccessToken = token,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Status = existingAgent != null ? "Updated" : "Registered"
        };
    }

    public async Task UpdateHeartbeatAsync(HeartbeatDto heartbeatDto)
    {
        var agent = await _context.Agents.FindAsync(heartbeatDto.AgentId);
        if (agent != null)
        {
            agent.LastSeen = DateTime.UtcNow;
            agent.Status = AgentStatus.Active;
            agent.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<PagedResult<AgentDto>> GetAgentsAsync(int page, int pageSize)
    {
        var query = _context.Agents.AsQueryable();
        var totalCount = await query.CountAsync();
        
        var agents = await query
            .OrderByDescending(a => a.LastSeen)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AgentDto
            {
                Id = a.Id,
                MachineId = a.MachineId,
                MachineName = a.MachineName,
                UserName = a.UserName,
                OsVersion = a.OsVersion,
                AgentVersion = a.AgentVersion,
                LastSeen = a.LastSeen,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync();

        return new PagedResult<AgentDto>
        {
            Items = agents,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AgentDto?> GetAgentAsync(Guid agentId)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent == null) return null;

        return new AgentDto
        {
            Id = agent.Id,
            MachineId = agent.MachineId,
            MachineName = agent.MachineName,
            UserName = agent.UserName,
            OsVersion = agent.OsVersion,
            AgentVersion = agent.AgentVersion,
            LastSeen = agent.LastSeen,
            Status = agent.Status,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }

    public async Task UpdateAgentStatusAsync(Guid agentId, string status)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent != null)
        {
            if (Enum.TryParse<AgentStatus>(status, out var parsedStatus))
            {
                agent.Status = parsedStatus;
                agent.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task DeleteAgentAsync(Guid agentId)
    {
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent != null)
        {
            _context.Agents.Remove(agent);
            await _context.SaveChangesAsync();
        }
    }
}