using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using EAM.API.Core.Models.Auth;
using EAM.Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;

namespace EAM.Infrastructure.Security;

/// <summary>
/// Serviço de autenticação
/// </summary>
public class AuthService : IAuthService
{
    private readonly ITokenService _tokenService;
    private readonly IRedisCacheService _cacheService;
    private readonly SecuritySettings _securitySettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ITokenService tokenService,
        IRedisCacheService cacheService,
        IOptions<SecuritySettings> securitySettings,
        ILogger<AuthService> logger)
    {
        _tokenService = tokenService;
        _cacheService = cacheService;
        _securitySettings = securitySettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Autentica um usuário com credenciais
    /// </summary>
    public async Task<AuthResult> AuthenticateAsync(string username, string password, string? ipAddress = null)
    {
        try
        {
            // Verificar se o usuário está bloqueado
            if (await IsUserLockedOutAsync(username))
            {
                _logger.LogWarning("Tentativa de login em conta bloqueada: {Username} de {IP}", username, ipAddress);
                return AuthResult.Failure("Conta temporariamente bloqueada devido a tentativas excessivas");
            }

            // Simular validação de usuário (em produção, buscar do banco de dados)
            var user = await GetUserAsync(username);
            if (user == null)
            {
                await RecordFailedLoginAttemptAsync(username, ipAddress);
                _logger.LogWarning("Tentativa de login com usuário inexistente: {Username} de {IP}", username, ipAddress);
                return AuthResult.Failure("Credenciais inválidas");
            }

            // Verificar senha
            if (!VerifyPassword(password, user.PasswordHash))
            {
                await RecordFailedLoginAttemptAsync(username, ipAddress);
                _logger.LogWarning("Tentativa de login com senha inválida: {Username} de {IP}", username, ipAddress);
                return AuthResult.Failure("Credenciais inválidas");
            }

            // Limpar tentativas falhadas
            await ClearFailedLoginAttemptsAsync(username);

            // Gerar tokens
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("role", user.Role),
                new Claim("agent_id", user.AgentId ?? ""),
                new Claim("ip_address", ipAddress ?? "unknown")
            };

            var token = _tokenService.GenerateToken(claims);
            var refreshToken = await GenerateRefreshTokenAsync(user.Id);

            // Armazenar sessão
            await StoreSessionAsync(user.Id, token, refreshToken, ipAddress);

            _logger.LogInformation("Login bem-sucedido: {Username} de {IP}", username, ipAddress);

            return AuthResult.Success(token, refreshToken, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante autenticação para {Username}", username);
            return AuthResult.Failure("Erro interno do servidor");
        }
    }

    /// <summary>
    /// Autentica um agente com chave API
    /// </summary>
    public async Task<AuthResult> AuthenticateAgentAsync(string agentKey, string? ipAddress = null)
    {
        try
        {
            // Verificar se a chave está válida
            if (!await IsValidAgentKeyAsync(agentKey))
            {
                _logger.LogWarning("Tentativa de autenticação com chave de agente inválida de {IP}", ipAddress);
                return AuthResult.Failure("Chave de agente inválida");
            }

            // Buscar informações do agente
            var agentInfo = await GetAgentInfoAsync(agentKey);
            if (agentInfo == null)
            {
                _logger.LogWarning("Agente não encontrado para chave de {IP}", ipAddress);
                return AuthResult.Failure("Agente não encontrado");
            }

            // Gerar claims para o agente
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, agentInfo.Id.ToString()),
                new Claim(ClaimTypes.Name, agentInfo.Name),
                new Claim("role", "agent"),
                new Claim("agent_id", agentInfo.Id.ToString()),
                new Claim("machine_name", agentInfo.MachineName),
                new Claim("ip_address", ipAddress ?? "unknown")
            };

            var token = _tokenService.GenerateToken(claims);
            var refreshToken = await GenerateRefreshTokenAsync(agentInfo.Id);

            // Armazenar sessão do agente
            await StoreAgentSessionAsync(agentInfo.Id, token, refreshToken, ipAddress);

            _logger.LogInformation("Autenticação de agente bem-sucedida: {AgentName} de {IP}", agentInfo.Name, ipAddress);

            return AuthResult.Success(token, refreshToken, agentInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante autenticação do agente");
            return AuthResult.Failure("Erro interno do servidor");
        }
    }

    /// <summary>
    /// Renova um token usando refresh token
    /// </summary>
    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, string? ipAddress = null)
    {
        try
        {
            // Verificar se o refresh token existe e é válido
            var sessionKey = $"refresh_token:{refreshToken}";
            var sessionData = await _cacheService.GetAsync<RefreshTokenSession>(sessionKey);
            
            if (sessionData == null || sessionData.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Tentativa de renovação com refresh token inválido ou expirado de {IP}", ipAddress);
                return AuthResult.Failure("Refresh token inválido ou expirado");
            }

            // Buscar informações do usuário
            var user = await GetUserByIdAsync(sessionData.UserId);
            if (user == null)
            {
                _logger.LogWarning("Usuário não encontrado durante renovação de token: {UserId}", sessionData.UserId);
                return AuthResult.Failure("Usuário não encontrado");
            }

            // Verificar se o IP mudou (opcional - pode ser configurável)
            if (!string.IsNullOrEmpty(sessionData.IpAddress) && 
                !string.IsNullOrEmpty(ipAddress) && 
                sessionData.IpAddress != ipAddress)
            {
                _logger.LogWarning("IP alterado durante renovação de token: {UserId} de {OldIP} para {NewIP}", 
                    sessionData.UserId, sessionData.IpAddress, ipAddress);
            }

            // Gerar novo token
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("role", user.Role),
                new Claim("agent_id", user.AgentId ?? ""),
                new Claim("ip_address", ipAddress ?? "unknown")
            };

            var newToken = _tokenService.GenerateToken(claims);
            var newRefreshToken = await GenerateRefreshTokenAsync(user.Id);

            // Remover o refresh token antigo
            await _cacheService.RemoveAsync(sessionKey);

            // Armazenar nova sessão
            await StoreSessionAsync(user.Id, newToken, newRefreshToken, ipAddress);

            _logger.LogInformation("Token renovado com sucesso: {Username} de {IP}", user.Username, ipAddress);

            return AuthResult.Success(newToken, newRefreshToken, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante renovação de token");
            return AuthResult.Failure("Erro interno do servidor");
        }
    }

    /// <summary>
    /// Invalida um token
    /// </summary>
    public async Task<bool> InvalidateTokenAsync(string token)
    {
        try
        {
            var principal = _tokenService.GetPrincipalFromToken(token);
            if (principal == null)
            {
                return false;
            }

            var jti = principal.FindFirst("jti")?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                return false;
            }

            // Adicionar token à blacklist
            var blacklistKey = $"blacklist:{jti}";
            var expirationTime = _tokenService.GetTokenExpiration(token);
            var ttl = expirationTime - DateTime.UtcNow;

            if (ttl > TimeSpan.Zero)
            {
                await _cacheService.SetAsync(blacklistKey, true, ttl);
            }

            _logger.LogInformation("Token invalidado: {JTI}", jti);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao invalidar token");
            return false;
        }
    }

    /// <summary>
    /// Verifica se um token está na blacklist
    /// </summary>
    public async Task<bool> IsTokenBlacklistedAsync(string token)
    {
        try
        {
            var principal = _tokenService.GetPrincipalFromToken(token);
            if (principal == null)
            {
                return true; // Token inválido = blacklisted
            }

            var jti = principal.FindFirst("jti")?.Value;
            if (string.IsNullOrEmpty(jti))
            {
                return true;
            }

            var blacklistKey = $"blacklist:{jti}";
            var isBlacklisted = await _cacheService.ExistsAsync(blacklistKey);

            return isBlacklisted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar blacklist do token");
            return true; // Em caso de erro, considerar blacklisted por segurança
        }
    }

    /// <summary>
    /// Faz logout de um usuário
    /// </summary>
    public async Task<bool> LogoutAsync(string token, string? refreshToken = null)
    {
        try
        {
            var success = true;

            // Invalidar access token
            if (!await InvalidateTokenAsync(token))
            {
                success = false;
            }

            // Invalidar refresh token se fornecido
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refreshTokenKey = $"refresh_token:{refreshToken}";
                await _cacheService.RemoveAsync(refreshTokenKey);
            }

            // Remover sessão ativa
            var principal = _tokenService.GetPrincipalFromToken(token);
            if (principal != null)
            {
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var sessionKey = $"session:{userId}";
                    await _cacheService.RemoveAsync(sessionKey);
                }
            }

            _logger.LogInformation("Logout realizado");
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante logout");
            return false;
        }
    }

    /// <summary>
    /// Valida se um usuário está bloqueado
    /// </summary>
    private async Task<bool> IsUserLockedOutAsync(string username)
    {
        var lockoutKey = $"lockout:{username}";
        return await _cacheService.ExistsAsync(lockoutKey);
    }

    /// <summary>
    /// Registra tentativa de login falhada
    /// </summary>
    private async Task RecordFailedLoginAttemptAsync(string username, string? ipAddress)
    {
        var attemptsKey = $"login_attempts:{username}";
        var attempts = await _cacheService.GetAsync<int>(attemptsKey);
        attempts++;

        await _cacheService.SetAsync(attemptsKey, attempts, TimeSpan.FromMinutes(_securitySettings.LockoutTimeMinutes));

        if (attempts >= _securitySettings.MaxLoginAttempts)
        {
            var lockoutKey = $"lockout:{username}";
            await _cacheService.SetAsync(lockoutKey, true, TimeSpan.FromMinutes(_securitySettings.LockoutTimeMinutes));
            
            _logger.LogWarning("Usuário bloqueado após {Attempts} tentativas: {Username} de {IP}", 
                attempts, username, ipAddress);
        }
    }

    /// <summary>
    /// Limpa tentativas de login falhadas
    /// </summary>
    private async Task ClearFailedLoginAttemptsAsync(string username)
    {
        var attemptsKey = $"login_attempts:{username}";
        await _cacheService.RemoveAsync(attemptsKey);
    }

    /// <summary>
    /// Gera um refresh token único
    /// </summary>
    private async Task<string> GenerateRefreshTokenAsync(int userId)
    {
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }

        var refreshToken = Convert.ToBase64String(tokenBytes);
        
        // Armazenar informações do refresh token
        var sessionKey = $"refresh_token:{refreshToken}";
        var sessionData = new RefreshTokenSession
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_securitySettings.RefreshTokenExpiryDays)
        };

        await _cacheService.SetAsync(sessionKey, sessionData, TimeSpan.FromDays(_securitySettings.RefreshTokenExpiryDays));

        return refreshToken;
    }

    /// <summary>
    /// Armazena informações da sessão
    /// </summary>
    private async Task StoreSessionAsync(int userId, string token, string refreshToken, string? ipAddress)
    {
        var sessionKey = $"session:{userId}";
        var sessionData = new UserSession
        {
            UserId = userId,
            Token = token,
            RefreshToken = refreshToken,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };

        await _cacheService.SetAsync(sessionKey, sessionData, TimeSpan.FromMinutes(_securitySettings.SessionLifetimeMinutes));
    }

    /// <summary>
    /// Armazena informações da sessão do agente
    /// </summary>
    private async Task StoreAgentSessionAsync(int agentId, string token, string refreshToken, string? ipAddress)
    {
        var sessionKey = $"agent_session:{agentId}";
        var sessionData = new AgentSession
        {
            AgentId = agentId,
            Token = token,
            RefreshToken = refreshToken,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };

        await _cacheService.SetAsync(sessionKey, sessionData, TimeSpan.FromMinutes(_securitySettings.SessionLifetimeMinutes));
    }

    /// <summary>
    /// Verifica se uma chave de agente é válida
    /// </summary>
    private async Task<bool> IsValidAgentKeyAsync(string agentKey)
    {
        // Em desenvolvimento, permitir chave padrão
        if (!string.IsNullOrEmpty(_securitySettings.DefaultAgentKey) && 
            agentKey == _securitySettings.DefaultAgentKey)
        {
            return true;
        }

        // Verificar nas chaves configuradas
        if (_securitySettings.ApiKeys.ContainsKey(agentKey))
        {
            return true;
        }

        // Verificar no cache (chaves dinâmicas)
        var keyExists = await _cacheService.ExistsAsync($"agent_key:{agentKey}");
        return keyExists;
    }

    /// <summary>
    /// Obtém informações do agente pela chave
    /// </summary>
    private async Task<AgentInfo?> GetAgentInfoAsync(string agentKey)
    {
        // Simular busca do agente (em produção, buscar do banco de dados)
        var agentData = await _cacheService.GetAsync<AgentInfo>($"agent_info:{agentKey}");
        
        if (agentData == null)
        {
            // Criar agente padrão para desenvolvimento
            agentData = new AgentInfo
            {
                Id = 1,
                Name = "Development Agent",
                MachineName = Environment.MachineName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        return agentData;
    }

    /// <summary>
    /// Obtém usuário por username (simulado)
    /// </summary>
    private async Task<UserInfo?> GetUserAsync(string username)
    {
        // Simular busca de usuário (em produção, buscar do banco de dados)
        await Task.Delay(1); // Simular async call

        if (username == "admin")
        {
            return new UserInfo
            {
                Id = 1,
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = HashPassword("admin123"), // Senha padrão para desenvolvimento
                Role = "admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        return null;
    }

    /// <summary>
    /// Obtém usuário por ID (simulado)
    /// </summary>
    private async Task<UserInfo?> GetUserByIdAsync(int userId)
    {
        // Simular busca de usuário (em produção, buscar do banco de dados)
        await Task.Delay(1); // Simular async call

        if (userId == 1)
        {
            return new UserInfo
            {
                Id = 1,
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = HashPassword("admin123"),
                Role = "admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        return null;
    }

    /// <summary>
    /// Verifica se a senha está correta
    /// </summary>
    private bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }

    /// <summary>
    /// Gera hash da senha
    /// </summary>
    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, _securitySettings.PasswordHashWorkFactor);
    }
}

/// <summary>
/// Informações do usuário
/// </summary>
public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string? AgentId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Informações do agente
/// </summary>
public class AgentInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Sessão do usuário
/// </summary>
public class UserSession
{
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
}

/// <summary>
/// Sessão do agente
/// </summary>
public class AgentSession
{
    public int AgentId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
}

/// <summary>
/// Sessão do refresh token
/// </summary>
public class RefreshTokenSession
{
    public int UserId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}