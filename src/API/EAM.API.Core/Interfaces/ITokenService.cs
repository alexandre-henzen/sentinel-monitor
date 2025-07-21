using EAM.Infrastructure.Security.Models;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para serviços de token JWT
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Gera token JWT para um usuário autenticado
    /// </summary>
    /// <param name="user">Usuário autenticado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Token JWT</returns>
    Task<string> GenerateTokenAsync(AuthenticatedUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida um token JWT
    /// </summary>
    /// <param name="token">Token a ser validado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da validação</returns>
    Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera refresh token
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Refresh token</returns>
    Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoga um token (adiciona à blacklist)
    /// </summary>
    /// <param name="token">Token a ser revogado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se revogado com sucesso</returns>
    Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se um token está na blacklist
    /// </summary>
    /// <param name="token">Token a ser verificado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se está na blacklist</returns>
    Task<bool> IsTokenBlacklistedAsync(string token, CancellationToken cancellationToken = default);
}