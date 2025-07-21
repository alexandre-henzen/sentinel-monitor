using EAM.Infrastructure.Security.Models;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para serviços de autenticação
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Autentica um usuário
    /// </summary>
    /// <param name="request">Requisição de autenticação</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da autenticação</returns>
    Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renova um token usando refresh token
    /// </summary>
    /// <param name="refreshToken">Refresh token</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da autenticação</returns>
    Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Faz logout de um usuário
    /// </summary>
    /// <param name="token">Token a ser invalidado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se logout foi bem-sucedido</returns>
    Task<bool> LogoutAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida credenciais de um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="agentKey">Chave do agente</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da validação</returns>
    Task<AuthenticationResult> ValidateAgentCredentialsAsync(
        Guid agentId, 
        string agentKey, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas de autenticação
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de autenticação</returns>
    Task<AuthenticationStatistics> GetAuthenticationStatisticsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista sessões ativas de um usuário
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de sessões ativas</returns>
    Task<List<SessionInfo>> GetActiveSessionsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoga todas as sessões de um usuário
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de sessões revogadas</returns>
    Task<int> RevokeAllUserSessionsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);
}