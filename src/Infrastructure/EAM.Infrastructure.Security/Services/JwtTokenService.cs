using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Security.Models;

namespace EAM.Infrastructure.Security.Services;

/// <summary>
/// Serviço para geração e validação de tokens JWT
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly SecuritySettings _securitySettings;
    private readonly ICacheService _cacheService;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly TokenValidationParameters _tokenValidationParameters;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="securitySettings">Configurações de segurança</param>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="logger">Logger</param>
    public JwtTokenService(
        IOptions<SecuritySettings> securitySettings,
        ICacheService cacheService,
        ILogger<JwtTokenService> logger)
    {
        _securitySettings = securitySettings.Value;
        _cacheService = cacheService;
        _logger = logger;
        _tokenHandler = new JwtSecurityTokenHandler();

        // Configurar parâmetros de validação
        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securitySettings.JwtSecretKey)),
            ValidateIssuer = true,
            ValidIssuer = _securitySettings.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = _securitySettings.JwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(_securitySettings.JwtClockSkewMinutes)
        };
    }

    /// <summary>
    /// Gera token JWT para um usuário autenticado
    /// </summary>
    /// <param name="user">Usuário autenticado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Token JWT</returns>
    public async Task<string> GenerateTokenAsync(AuthenticatedUser user, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Gerando token JWT para usuário {UserId}", user.Id);

            var sessionId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(_securitySettings.JwtExpiryMinutes);

            // Criar claims
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Name, user.Name),
                new(JwtRegisteredClaimNames.Jti, sessionId),
                new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new(CustomClaims.UserId, user.Id.ToString()),
                new(CustomClaims.UserType, user.UserType.ToString()),
                new(CustomClaims.SessionId, sessionId),
                new(CustomClaims.AuthTime, now.ToString("O"))
            };

            // Adicionar roles como claims
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Adicionar permissões
            if (user.Permissions.Any())
            {
                claims.Add(new Claim(CustomClaims.Permissions, JsonSerializer.Serialize(user.Permissions)));
            }

            // Adicionar dados adicionais
            if (user.AdditionalData.Any())
            {
                foreach (var kvp in user.AdditionalData)
                {
                    if (kvp.Value != null)
                    {
                        claims.Add(new Claim(kvp.Key, kvp.Value.ToString() ?? string.Empty));
                    }
                }
            }

            // Criar token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securitySettings.JwtSecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = _securitySettings.JwtIssuer,
                Audience = _securitySettings.JwtAudience,
                SigningCredentials = credentials
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = _tokenHandler.WriteToken(token);

            // Armazenar sessão no cache
            var sessionInfo = new SessionInfo
            {
                SessionId = sessionId,
                UserId = user.Id,
                CreatedAt = now,
                LastActivityAt = now,
                ExpiresAt = expiresAt,
                ClientIpAddress = user.LastIpAddress,
                IsActive = true
            };

            await _cacheService.SetAsync(
                $"session:{sessionId}",
                sessionInfo,
                TimeSpan.FromMinutes(_securitySettings.JwtExpiryMinutes),
                cancellationToken);

            _logger.LogInformation("Token JWT gerado para usuário {UserId} com sessão {SessionId}",
                user.Id, sessionId);

            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar token JWT para usuário {UserId}", user.Id);
            throw;
        }
    }

    /// <summary>
    /// Valida um token JWT
    /// </summary>
    /// <param name="token">Token a ser validado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da validação</returns>
    public async Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token não fornecido"
                };
            }

            // Remover prefixo "Bearer " se presente
            if (token.StartsWith("Bearer "))
            {
                token = token.Substring(7);
            }

            // Validar formato do token
            if (!_tokenHandler.CanReadToken(token))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Formato de token inválido"
                };
            }

            // Validar token
            var principal = _tokenHandler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);
            
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token JWT inválido"
                };
            }

            // Extrair claims
            var claims = principal.Claims.ToDictionary(c => c.Type, c => (object)c.Value);
            
            // Verificar se a sessão ainda está ativa
            var sessionId = principal.FindFirst(CustomClaims.SessionId)?.Value;
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionInfo = await _cacheService.GetAsync<SessionInfo>($"session:{sessionId}", cancellationToken);
                if (sessionInfo == null || !sessionInfo.IsActive)
                {
                    return new TokenValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Sessão expirada ou inválida"
                    };
                }

                // Atualizar última atividade
                sessionInfo.LastActivityAt = DateTime.UtcNow;
                await _cacheService.SetAsync(
                    $"session:{sessionId}",
                    sessionInfo,
                    TimeSpan.FromMinutes(_securitySettings.JwtExpiryMinutes),
                    cancellationToken);
            }

            // Criar objeto do usuário autenticado
            var user = CreateAuthenticatedUserFromClaims(principal.Claims);

            // Calcular tempo até expiração
            var expirationTime = jwtToken.ValidTo;
            var timeUntilExpiry = expirationTime - DateTime.UtcNow;
            var requiresRenewal = timeUntilExpiry.TotalMinutes < _securitySettings.TokenRenewalThresholdMinutes;

            return new TokenValidationResult
            {
                IsValid = true,
                User = user,
                Claims = claims,
                IsExpired = false,
                TimeUntilExpiry = timeUntilExpiry,
                RequiresRenewal = requiresRenewal
            };
        }
        catch (SecurityTokenExpiredException)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                IsExpired = true,
                ErrorMessage = "Token expirado"
            };
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("Token inválido: {Message}", ex.Message);
            
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Token inválido: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar token JWT");
            
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Erro interno na validação do token"
            };
        }
    }

    /// <summary>
    /// Gera refresh token
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Refresh token</returns>
    public async Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshToken = GenerateSecureRandomString(64);
            var expiresAt = DateTime.UtcNow.AddDays(_securitySettings.RefreshTokenExpiryDays);

            // Armazenar refresh token no cache
            var refreshTokenInfo = new
            {
                UserId = userId,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed = false
            };

            await _cacheService.SetAsync(
                $"refresh-token:{refreshToken}",
                refreshTokenInfo,
                TimeSpan.FromDays(_securitySettings.RefreshTokenExpiryDays),
                cancellationToken);

            _logger.LogDebug("Refresh token gerado para usuário {UserId}", userId);

            return refreshToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar refresh token para usuário {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Revoga um token (adiciona à blacklist)
    /// </summary>
    /// <param name="token">Token a ser revogado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se revogado com sucesso</returns>
    public async Task<bool> RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
                return false;

            // Extrair JTI (ID único do token)
            var jwtToken = _tokenHandler.ReadJwtToken(token);
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            var sessionId = jwtToken.Claims.FirstOrDefault(c => c.Type == CustomClaims.SessionId)?.Value;

            if (!string.IsNullOrEmpty(jti))
            {
                // Adicionar à blacklist
                var expirationTime = jwtToken.ValidTo - DateTime.UtcNow;
                if (expirationTime.TotalSeconds > 0)
                {
                    await _cacheService.SetAsync(
                        $"blacklist:{jti}",
                        DateTime.UtcNow,
                        expirationTime,
                        cancellationToken);
                }
            }

            // Invalidar sessão
            if (!string.IsNullOrEmpty(sessionId))
            {
                var sessionInfo = await _cacheService.GetAsync<SessionInfo>($"session:{sessionId}", cancellationToken);
                if (sessionInfo != null)
                {
                    sessionInfo.IsActive = false;
                    await _cacheService.SetAsync(
                        $"session:{sessionId}",
                        sessionInfo,
                        TimeSpan.FromMinutes(5), // Manter por pouco tempo para auditoria
                        cancellationToken);
                }
            }

            _logger.LogInformation("Token revogado com sucesso");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao revogar token");
            return false;
        }
    }

    /// <summary>
    /// Verifica se um token está na blacklist
    /// </summary>
    /// <param name="token">Token a ser verificado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se está na blacklist</returns>
    public async Task<bool> IsTokenBlacklistedAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var jwtToken = _tokenHandler.ReadJwtToken(token);
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

            if (string.IsNullOrEmpty(jti))
                return false;

            return await _cacheService.ExistsAsync($"blacklist:{jti}", cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cria usuário autenticado a partir dos claims
    /// </summary>
    /// <param name="claims">Claims do token</param>
    /// <returns>Usuário autenticado</returns>
    private AuthenticatedUser CreateAuthenticatedUserFromClaims(IEnumerable<Claim> claims)
    {
        var claimsDict = claims.ToDictionary(c => c.Type, c => c.Value);
        
        var user = new AuthenticatedUser();

        if (claimsDict.TryGetValue(CustomClaims.UserId, out var userIdStr) && Guid.TryParse(userIdStr, out var userId))
        {
            user.Id = userId;
        }

        if (claimsDict.TryGetValue(ClaimTypes.Name, out var name))
        {
            user.Name = name;
        }

        if (claimsDict.TryGetValue(ClaimTypes.Email, out var email))
        {
            user.Email = email;
        }

        if (claimsDict.TryGetValue(CustomClaims.UserType, out var userTypeStr) && 
            Enum.TryParse<UserType>(userTypeStr, out var userType))
        {
            user.UserType = userType;
        }

        // Extrair roles
        user.Roles = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

        // Extrair permissões
        if (claimsDict.TryGetValue(CustomClaims.Permissions, out var permissionsJson))
        {
            try
            {
                user.Permissions = JsonSerializer.Deserialize<List<string>>(permissionsJson) ?? new List<string>();
            }
            catch
            {
                user.Permissions = new List<string>();
            }
        }

        return user;
    }

    /// <summary>
    /// Gera string aleatória segura
    /// </summary>
    /// <param name="length">Tamanho da string</param>
    /// <returns>String aleatória</returns>
    private string GenerateSecureRandomString(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "")[..length];
    }
}