using EAM.API.Core.Interfaces;
using EAM.API.Core.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace EAM.API.Controllers;

/// <summary>
/// Controlador de autenticação
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Autenticação de usuário
    /// </summary>
    /// <param name="request">Credenciais do usuário</param>
    /// <returns>Token de acesso e refresh token</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 400)]
    [ProducesResponseType(typeof(ErrorResponseDto), 401)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Message = "Dados inválidos",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            var ipAddress = GetClientIpAddress();
            var result = await _authService.AuthenticateAsync(request.Username, request.Password, ipAddress);

            if (!result.IsSuccess)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Message = result.ErrorMessage ?? "Credenciais inválidas"
                });
            }

            var response = new AuthResponseDto
            {
                AccessToken = result.AccessToken!,
                RefreshToken = result.RefreshToken!,
                ExpiresIn = 3600, // 1 hora
                TokenType = "Bearer",
                User = new UserDto
                {
                    Id = result.User?.Id ?? 0,
                    Username = result.User?.Username ?? "",
                    Email = result.User?.Email,
                    Role = result.User?.Role ?? "user"
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante login do usuário {Username}", request.Username);
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Erro interno do servidor"
            });
        }
    }

    /// <summary>
    /// Autenticação de agente
    /// </summary>
    /// <param name="request">Chave do agente</param>
    /// <returns>Token de acesso e refresh token</returns>
    [HttpPost("agent-login")]
    [ProducesResponseType(typeof(AuthResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 400)]
    [ProducesResponseType(typeof(ErrorResponseDto), 401)]
    public async Task<ActionResult<AuthResponseDto>> AgentLogin([FromBody] AgentLoginRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Message = "Dados inválidos",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            var ipAddress = GetClientIpAddress();
            var result = await _authService.AuthenticateAgentAsync(request.AgentKey, ipAddress);

            if (!result.IsSuccess)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Message = result.ErrorMessage ?? "Chave de agente inválida"
                });
            }

            var response = new AuthResponseDto
            {
                AccessToken = result.AccessToken!,
                RefreshToken = result.RefreshToken!,
                ExpiresIn = 3600, // 1 hora
                TokenType = "Bearer",
                Agent = new AgentDto
                {
                    Id = result.Agent?.Id ?? 0,
                    Name = result.Agent?.Name ?? "",
                    MachineName = result.Agent?.MachineName ?? "",
                    IsActive = result.Agent?.IsActive ?? false
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante login do agente");
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Erro interno do servidor"
            });
        }
    }

    /// <summary>
    /// Renovação de token
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>Novo token de acesso</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 400)]
    [ProducesResponseType(typeof(ErrorResponseDto), 401)]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Message = "Dados inválidos",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            var ipAddress = GetClientIpAddress();
            var result = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress);

            if (!result.IsSuccess)
            {
                return Unauthorized(new ErrorResponseDto
                {
                    Message = result.ErrorMessage ?? "Refresh token inválido"
                });
            }

            var response = new AuthResponseDto
            {
                AccessToken = result.AccessToken!,
                RefreshToken = result.RefreshToken!,
                ExpiresIn = 3600, // 1 hora
                TokenType = "Bearer",
                User = new UserDto
                {
                    Id = result.User?.Id ?? 0,
                    Username = result.User?.Username ?? "",
                    Email = result.User?.Email,
                    Role = result.User?.Role ?? "user"
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante renovação de token");
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Erro interno do servidor"
            });
        }
    }

    /// <summary>
    /// Logout do usuário
    /// </summary>
    /// <param name="request">Dados do logout</param>
    /// <returns>Confirmação do logout</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(SuccessResponseDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 400)]
    public async Task<ActionResult<SuccessResponseDto>> Logout([FromBody] LogoutRequestDto? request = null)
    {
        try
        {
            var token = ExtractTokenFromHeader();
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new ErrorResponseDto
                {
                    Message = "Token não encontrado"
                });
            }

            var refreshToken = request?.RefreshToken;
            var success = await _authService.LogoutAsync(token, refreshToken);

            if (!success)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Message = "Erro durante logout"
                });
            }

            return Ok(new SuccessResponseDto
            {
                Message = "Logout realizado com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante logout");
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Erro interno do servidor"
            });
        }
    }

    /// <summary>
    /// Validação de token
    /// </summary>
    /// <returns>Informações do usuário autenticado</returns>
    [HttpGet("validate")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfoDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 401)]
    public async Task<ActionResult<UserInfoDto>> ValidateToken()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst("role")?.Value;
            var agentId = User.FindFirst("agent_id")?.Value;

            var userInfo = new UserInfoDto
            {
                Id = int.TryParse(userId, out var id) ? id : 0,
                Username = username ?? "",
                Email = email,
                Role = role ?? "user",
                AgentId = agentId,
                IsAuthenticated = true,
                AuthenticatedAt = DateTime.UtcNow
            };

            return Ok(userInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante validação de token");
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Erro interno do servidor"
            });
        }
    }

    /// <summary>
    /// Obtém informações do usuário atual
    /// </summary>
    /// <returns>Informações detalhadas do usuário</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), 200)]
    [ProducesResponseType(typeof(ErrorResponseDto), 401)]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst("role")?.Value;
            var agentId = User.FindFirst("agent_id")?.Value;
            var ipAddress = User.FindFirst("ip_address")?.Value;

            var profile = new UserProfileDto
            {
                Id = int.TryParse(userId, out var id) ? id : 0,
                Username = username ?? "",
                Email = email,
                Role = role ?? "user",
                AgentId = agentId,
                LastLoginIpAddress = ipAddress,
                IsActive = true,
                AuthenticatedAt = DateTime.UtcNow
            };

            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter informações do usuário atual");
            return StatusCode(500, new ErrorResponseDto
            {
                Message = "Erro interno do servidor"
            });
        }
    }

    /// <summary>
    /// Extrai o token do header Authorization
    /// </summary>
    private string? ExtractTokenFromHeader()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        return authHeader.Substring("Bearer ".Length).Trim();
    }

    /// <summary>
    /// Obtém o endereço IP do cliente
    /// </summary>
    private string GetClientIpAddress()
    {
        var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            return xForwardedFor.Split(',')[0].Trim();
        }

        var xRealIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp))
        {
            return xRealIp;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// DTO para requisição de login
/// </summary>
public class LoginRequestDto
{
    [Required(ErrorMessage = "Nome de usuário é obrigatório")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Senha é obrigatória")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = false;
}

/// <summary>
/// DTO para requisição de login do agente
/// </summary>
public class AgentLoginRequestDto
{
    [Required(ErrorMessage = "Chave do agente é obrigatória")]
    public string AgentKey { get; set; } = string.Empty;
}

/// <summary>
/// DTO para requisição de refresh token
/// </summary>
public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh token é obrigatório")]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// DTO para requisição de logout
/// </summary>
public class LogoutRequestDto
{
    public string? RefreshToken { get; set; }
}

/// <summary>
/// DTO para resposta de autenticação
/// </summary>
public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public UserDto? User { get; set; }
    public AgentDto? Agent { get; set; }
}

/// <summary>
/// DTO para informações do usuário
/// </summary>
public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// DTO para informações do agente
/// </summary>
public class AgentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO para informações do usuário autenticado
/// </summary>
public class UserInfoDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? AgentId { get; set; }
    public bool IsAuthenticated { get; set; }
    public DateTime AuthenticatedAt { get; set; }
}

/// <summary>
/// DTO para perfil do usuário
/// </summary>
public class UserProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? AgentId { get; set; }
    public string? LastLoginIpAddress { get; set; }
    public bool IsActive { get; set; }
    public DateTime AuthenticatedAt { get; set; }
}

/// <summary>
/// DTO para resposta de erro
/// </summary>
public class ErrorResponseDto
{
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO para resposta de sucesso
/// </summary>
public class SuccessResponseDto
{
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}