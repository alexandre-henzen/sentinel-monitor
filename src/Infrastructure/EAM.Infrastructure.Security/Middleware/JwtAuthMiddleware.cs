using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace EAM.Infrastructure.Security.Middleware;

/// <summary>
/// Middleware para autenticação JWT
/// </summary>
public class JwtAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtAuthMiddleware> _logger;
    private readonly SecuritySettings _securitySettings;

    public JwtAuthMiddleware(
        RequestDelegate next,
        ILogger<JwtAuthMiddleware> logger,
        IOptions<SecuritySettings> securitySettings)
    {
        _next = next;
        _logger = logger;
        _securitySettings = securitySettings.Value;
    }

    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, IAuthService authService)
    {
        try
        {
            // Extrair token do header Authorization
            var token = ExtractTokenFromHeader(context);
            
            if (!string.IsNullOrEmpty(token))
            {
                await ProcessTokenAsync(context, token, tokenService, authService);
            }
            else
            {
                // Verificar se é um endpoint que requer autenticação
                if (RequiresAuthentication(context))
                {
                    await SetUnauthorizedResponse(context, "Token de acesso requerido");
                    return;
                }
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no middleware de autenticação JWT");
            await SetUnauthorizedResponse(context, "Erro interno de autenticação");
        }
    }

    /// <summary>
    /// Extrai o token do header Authorization
    /// </summary>
    private static string? ExtractTokenFromHeader(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authHeader))
            return null;

        // Verificar se é Bearer token
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return authHeader.Substring("Bearer ".Length).Trim();
    }

    /// <summary>
    /// Processa o token JWT
    /// </summary>
    private async Task ProcessTokenAsync(
        HttpContext context, 
        string token, 
        ITokenService tokenService, 
        IAuthService authService)
    {
        try
        {
            // Verificar se o token está na blacklist
            if (await authService.IsTokenBlacklistedAsync(token))
            {
                _logger.LogWarning("Tentativa de acesso com token blacklisted");
                await SetUnauthorizedResponse(context, "Token inválido");
                return;
            }

            // Validar o token
            var principal = tokenService.ValidateToken(token);
            if (principal == null)
            {
                _logger.LogWarning("Token JWT inválido");
                await SetUnauthorizedResponse(context, "Token inválido");
                return;
            }

            // Verificar se o token não expirou
            var exp = principal.FindFirst("exp")?.Value;
            if (!string.IsNullOrEmpty(exp))
            {
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp));
                if (expirationTime <= DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("Token JWT expirado");
                    await SetUnauthorizedResponse(context, "Token expirado");
                    return;
                }
            }

            // Definir o usuário no contexto
            context.User = principal;

            // Adicionar informações extras ao contexto
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            var role = principal.FindFirst("role")?.Value;

            context.Items["UserId"] = userId;
            context.Items["Username"] = username;
            context.Items["Role"] = role;
            context.Items["AuthenticatedAt"] = DateTime.UtcNow;

            _logger.LogDebug("Token validado com sucesso para usuário: {Username} ({UserId})", username, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao processar token JWT");
            await SetUnauthorizedResponse(context, "Token inválido");
        }
    }

    /// <summary>
    /// Verifica se o endpoint requer autenticação
    /// </summary>
    private static bool RequiresAuthentication(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        
        // Endpoints que não requerem autenticação
        var publicEndpoints = new[]
        {
            "/health",
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/refresh",
            "/swagger",
            "/api-docs",
            "/.well-known"
        };

        return publicEndpoints.All(endpoint => !path?.StartsWith(endpoint) == true);
    }

    /// <summary>
    /// Define resposta de não autorizado
    /// </summary>
    private static async Task SetUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = "Unauthorized",
            message = message,
            timestamp = DateTime.UtcNow,
            path = context.Request.Path.Value
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
}