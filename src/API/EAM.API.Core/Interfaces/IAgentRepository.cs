using EAM.API.Core.Entities;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para repositório de agentes
/// </summary>
public interface IAgentRepository
{
    /// <summary>
    /// Obtém um agente pelo ID
    /// </summary>
    /// <param name="id">ID do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Agente encontrado ou null</returns>
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém um agente pelo nome do computador e usuário
    /// </summary>
    /// <param name="computerName">Nome do computador</param>
    /// <param name="userName">Nome do usuário</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Agente encontrado ou null</returns>
    Task<Agent?> GetByComputerAndUserAsync(string computerName, string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todos os agentes ativos
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de agentes ativos</returns>
    Task<IEnumerable<Agent>> GetActiveAgentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém agentes por status
    /// </summary>
    /// <param name="status">Status do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de agentes com o status especificado</returns>
    Task<IEnumerable<Agent>> GetByStatusAsync(AgentStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém agentes que não enviaram heartbeat há mais de X minutos
    /// </summary>
    /// <param name="timeoutMinutes">Timeout em minutos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de agentes offline</returns>
    Task<IEnumerable<Agent>> GetOfflineAgentsAsync(int timeoutMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um novo agente
    /// </summary>
    /// <param name="agent">Dados do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Agente criado</returns>
    Task<Agent> CreateAsync(Agent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza um agente existente
    /// </summary>
    /// <param name="agent">Dados do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Agente atualizado</returns>
    Task<Agent> UpdateAsync(Agent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o heartbeat de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="heartbeatTime">Timestamp do heartbeat</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    Task<bool> UpdateHeartbeatAsync(Guid agentId, DateTime heartbeatTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza o status de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="status">Novo status</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    Task<bool> UpdateStatusAsync(Guid agentId, AgentStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um agente
    /// </summary>
    /// <param name="id">ID do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas dos agentes
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas dos agentes</returns>
    Task<AgentStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se um agente existe
    /// </summary>
    /// <param name="id">ID do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se existe</returns>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a configuração de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Configuração do agente</returns>
    Task<string?> GetConfigurationAsync(Guid agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza a configuração de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="configuration">Nova configuração em JSON</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se atualizado com sucesso</returns>
    Task<bool> UpdateConfigurationAsync(Guid agentId, string configuration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Estatísticas dos agentes
/// </summary>
public class AgentStatistics
{
    /// <summary>
    /// Total de agentes
    /// </summary>
    public int TotalAgents { get; set; }

    /// <summary>
    /// Agentes ativos
    /// </summary>
    public int ActiveAgents { get; set; }

    /// <summary>
    /// Agentes offline
    /// </summary>
    public int OfflineAgents { get; set; }

    /// <summary>
    /// Agentes inativos
    /// </summary>
    public int InactiveAgents { get; set; }

    /// <summary>
    /// Agentes em manutenção
    /// </summary>
    public int MaintenanceAgents { get; set; }

    /// <summary>
    /// Agentes com erro
    /// </summary>
    public int ErrorAgents { get; set; }

    /// <summary>
    /// Agentes desabilitados
    /// </summary>
    public int DisabledAgents { get; set; }

    /// <summary>
    /// Último heartbeat recebido
    /// </summary>
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// Distribuição por versão
    /// </summary>
    public Dictionary<string, int> VersionDistribution { get; set; } = new();

    /// <summary>
    /// Distribuição por sistema operacional
    /// </summary>
    public Dictionary<string, int> OsDistribution { get; set; } = new();
}