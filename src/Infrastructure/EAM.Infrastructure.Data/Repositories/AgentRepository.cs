using Microsoft.EntityFrameworkCore;
using EAM.API.Core.Entities;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Data.Context;

namespace EAM.Infrastructure.Data.Repositories;

/// <summary>
/// Implementação do repositório de agentes
/// </summary>
public class AgentRepository : IAgentRepository
{
    private readonly EamDbContext _context;

    /// <summary>
    /// Construtor do repositório
    /// </summary>
    /// <param name="context">Contexto do banco de dados</param>
    public AgentRepository(EamDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtém um agente pelo ID
    /// </summary>
    /// <param name="id">ID do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Agente encontrado ou null</returns>
    public async Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <summary>
    /// Obtém um agente pelo nome do computador e usuário
    /// </summary>
    /// <param name="computerName">Nome do computador</param>
    /// <param name="userName">Nome do usuário</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Agente encontrado ou null</returns>
    public async Task<Agent?> GetByComputerAndUserAsync(string computerName, string userName, CancellationToken cancellationToken = default)
    {
        return await _context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ComputerName == computerName && a.UserName == userName, cancellationToken);
    }

    /// <summary>
    /// Obtém todos os agentes ativos
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de agentes ativos</returns>
    public async Task<IEnumerable<Agent>> GetActiveAgentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Agents
            .AsNoTracking()
            .Where(a => a.IsActive && a.Status == AgentStatus.Active)
            .OrderBy(a => a.ComputerName)
            .ThenBy(a => a.UserName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém agentes por status
    /// </summary>
    /// <param name="status">Status do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de agentes com o status especificado</returns>
    public async Task<IEnumerable<Agent>> GetByStatusAsync(AgentStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Agents
            .AsNoTracking()
            .Where(a => a.Status == status)
            .OrderBy(a => a.ComputerName)
            .ThenBy(a => a.UserName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém agentes que não enviaram heartbeat há mais de X minutos
    /// </summary>
    /// <param name="timeoutMinutes">Timeout em minutos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de agentes offline</returns>
    public async Task<IEnumerable<Agent>> GetOfflineAgentsAsync(int timeoutMinutes, CancellationToken cancellationToken = default)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
        
        return await _context.Agents
            .AsNoTracking()
            .Where(a => a.LastHeartbeat < cutoffTime && a.Status == AgentStatus.Active)
            .OrderBy(a => a.LastHeartbeat)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Cria um novo agente
    /// </summary>
    /// <param name="agent">Dados do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Agente criado</returns>
    public async Task<Agent> CreateAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        _context.Agents.Add(agent);
        await _context.SaveChangesAsync(cancellationToken);
        return agent;
    }

    /// <summary>
    /// Atualiza um agente existente
    /// </summary>
    /// <param name="agent">Dados do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Agente atualizado</returns>
    public async Task<Agent> UpdateAsync(Agent agent, CancellationToken cancellationToken = default)
    {
        _context.Agents.Update(agent);
        await _context.SaveChangesAsync(cancellationToken);
        return agent;
    }

    /// <summary>
    /// Atualiza o heartbeat de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="heartbeatTime">Timestamp do heartbeat</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    public async Task<bool> UpdateHeartbeatAsync(Guid agentId, DateTime heartbeatTime, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Agents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.LastHeartbeat, heartbeatTime)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Atualiza o status de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="status">Novo status</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    public async Task<bool> UpdateStatusAsync(Guid agentId, AgentStatus status, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Agents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, status)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Remove um agente
    /// </summary>
    /// <param name="id">ID do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Agents
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Obtém estatísticas dos agentes
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas dos agentes</returns>
    public async Task<AgentStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalAgents = await _context.Agents.CountAsync(cancellationToken);
        var activeAgents = await _context.Agents.CountAsync(a => a.Status == AgentStatus.Active, cancellationToken);
        var offlineAgents = await _context.Agents.CountAsync(a => a.Status == AgentStatus.Offline, cancellationToken);
        var inactiveAgents = await _context.Agents.CountAsync(a => a.Status == AgentStatus.Inactive, cancellationToken);
        var maintenanceAgents = await _context.Agents.CountAsync(a => a.Status == AgentStatus.Maintenance, cancellationToken);
        var errorAgents = await _context.Agents.CountAsync(a => a.Status == AgentStatus.Error, cancellationToken);
        var disabledAgents = await _context.Agents.CountAsync(a => a.Status == AgentStatus.Disabled, cancellationToken);

        var lastHeartbeat = await _context.Agents
            .Where(a => a.Status == AgentStatus.Active)
            .MaxAsync(a => (DateTime?)a.LastHeartbeat, cancellationToken);

        var versionDistribution = await _context.Agents
            .GroupBy(a => a.Version)
            .Select(g => new { Version = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Version, x => x.Count, cancellationToken);

        var osDistribution = await _context.Agents
            .GroupBy(a => a.OsVersion)
            .Select(g => new { OsVersion = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OsVersion, x => x.Count, cancellationToken);

        return new AgentStatistics
        {
            TotalAgents = totalAgents,
            ActiveAgents = activeAgents,
            OfflineAgents = offlineAgents,
            InactiveAgents = inactiveAgents,
            MaintenanceAgents = maintenanceAgents,
            ErrorAgents = errorAgents,
            DisabledAgents = disabledAgents,
            LastHeartbeat = lastHeartbeat,
            VersionDistribution = versionDistribution,
            OsDistribution = osDistribution
        };
    }

    /// <summary>
    /// Verifica se um agente existe
    /// </summary>
    /// <param name="id">ID do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se existe</returns>
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Agents
            .AnyAsync(a => a.Id == id, cancellationToken);
    }

    /// <summary>
    /// Obtém a configuração de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Configuração do agente</returns>
    public async Task<string?> GetConfigurationAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _context.Agents
            .AsNoTracking()
            .Where(a => a.Id == agentId)
            .Select(a => new { a.Configuration })
            .FirstOrDefaultAsync(cancellationToken);

        return agent?.Configuration;
    }

    /// <summary>
    /// Atualiza a configuração de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="configuration">Nova configuração em JSON</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    public async Task<bool> UpdateConfigurationAsync(Guid agentId, string configuration, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.Agents
            .Where(a => a.Id == agentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Configuration, configuration)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), cancellationToken);

        return rowsAffected > 0;
    }
}