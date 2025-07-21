using EAM.API.Core.Models.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EAM.Infrastructure.Telemetry.Middleware;

/// <summary>
/// Middleware para coleta de métricas e telemetria
/// </summary>
public class TelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TelemetryMiddleware> _logger;
    private readonly EamMetrics _metrics;

    public TelemetryMiddleware(
        RequestDelegate next,
        ILogger<TelemetryMiddleware> logger,
        EamMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";
        var requestId = context.TraceIdentifier;

        // Criar activity para tracing
        using var activity = EamActivitySource.StartActivity($"HTTP {method} {path}", ActivityKind.Server);
        
        // Adicionar informações ao contexto da activity
        activity?.SetTag("http.method", method);
        activity?.SetTag("http.url", context.Request.GetDisplayUrl());
        activity?.SetTag("http.scheme", context.Request.Scheme);
        activity?.SetTag("http.host", context.Request.Host.Value);
        activity?.SetTag("http.target", path);
        activity?.SetTag("http.user_agent", context.Request.Headers["User-Agent"].ToString());
        activity?.SetTag("http.request_id", requestId);

        // Adicionar informações do usuário se disponível
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var username = context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            
            activity?.SetTag("user.id", userId);
            activity?.SetTag("user.name", username);
        }

        // Adicionar contexto de telemetria personalizado
        var telemetryContext = new TelemetryContext
        {
            RequestId = requestId,
            UserId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            AgentId = context.User?.FindFirst("agent_id")?.Value,
            IpAddress = GetClientIpAddress(context),
            UserAgent = context.Request.Headers["User-Agent"].ToString(),
            Timestamp = DateTime.UtcNow
        };

        // Adicionar ao contexto do HTTP
        context.Items["TelemetryContext"] = telemetryContext;

        try
        {
            // Executar próximo middleware
            await _next(context);
        }
        catch (Exception ex)
        {
            // Registrar erro na activity
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);

            // Log estruturado do erro
            _logger.LogError(ex, "Erro durante processamento da requisição {RequestId} {Method} {Path}", 
                requestId, method, path);

            throw;
        }
        finally
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed.TotalSeconds;
            var statusCode = context.Response.StatusCode;

            // Atualizar informações da activity
            activity?.SetTag("http.status_code", statusCode);
            activity?.SetTag("http.response_size", context.Response.ContentLength ?? 0);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

            // Registrar métricas
            _metrics.RecordApiRequest(method, path, statusCode);
            _metrics.RecordRequestDuration(duration, method, path);

            // Log estruturado da requisição
            _logger.LogInformation(
                "Requisição processada: {RequestId} {Method} {Path} {StatusCode} {Duration}ms {UserAgent} {IpAddress}",
                requestId, method, path, statusCode, stopwatch.ElapsedMilliseconds, 
                telemetryContext.UserAgent, telemetryContext.IpAddress);

            // Definir status da activity baseado no código de resposta
            if (statusCode >= 400)
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"HTTP {statusCode}");
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
        }
    }

    /// <summary>
    /// Obtém o endereço IP do cliente
    /// </summary>
    private static string GetClientIpAddress(HttpContext context)
    {
        // Verificar headers de proxy
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            return xForwardedFor.Split(',')[0].Trim();
        }

        var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp))
        {
            return xRealIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Middleware para coleta de métricas de performance
/// </summary>
public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly EamMetrics _metrics;

    public PerformanceMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMiddleware> logger,
        EamMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var initialMemory = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryDelta = finalMemory - initialMemory;

            // Registrar métricas de performance se a requisição for lenta
            if (stopwatch.ElapsedMilliseconds > 1000) // Mais de 1 segundo
            {
                _logger.LogWarning(
                    "Requisição lenta detectada: {Method} {Path} {Duration}ms {MemoryDelta}bytes",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    memoryDelta);
            }

            // Atualizar métricas de memória
            _metrics.SetMemoryUsage(finalMemory);
        }
    }
}

/// <summary>
/// Middleware para coleta de métricas de segurança
/// </summary>
public class SecurityTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityTelemetryMiddleware> _logger;
    private readonly EamMetrics _metrics;

    public SecurityTelemetryMiddleware(
        RequestDelegate next,
        ILogger<SecurityTelemetryMiddleware> logger,
        EamMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var ipAddress = GetClientIpAddress(context);

        // Detectar tentativas de ataques comuns
        var isSuspicious = DetectSuspiciousActivity(path, method, userAgent, context.Request.Headers);

        if (isSuspicious)
        {
            using var activity = EamActivitySource.StartActivity("Security.SuspiciousActivity", ActivityKind.Server);
            activity?.SetTag("security.threat_detected", true);
            activity?.SetTag("security.threat_type", "suspicious_request");
            activity?.SetTag("http.method", method);
            activity?.SetTag("http.path", path);
            activity?.SetTag("http.user_agent", userAgent);
            activity?.SetTag("client.ip", ipAddress);

            _logger.LogWarning(
                "Atividade suspeita detectada: {Method} {Path} {UserAgent} {IpAddress}",
                method, path, userAgent, ipAddress);
        }

        // Registrar tentativas de autenticação
        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.OnCompleted(() =>
            {
                var isSuccess = context.Response.StatusCode < 400;
                var authMethod = path.Contains("login") ? "login" : 
                                path.Contains("agent-login") ? "agent-login" : "other";
                
                _metrics.RecordAuthenticationAttempt(authMethod, isSuccess);
                
                if (!isSuccess)
                {
                    _logger.LogWarning(
                        "Tentativa de autenticação falhada: {Method} {AuthMethod} {StatusCode} {IpAddress}",
                        method, authMethod, context.Response.StatusCode, ipAddress);
                }
                
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }

    /// <summary>
    /// Detecta atividades suspeitas
    /// </summary>
    private static bool DetectSuspiciousActivity(string path, string method, string userAgent, IHeaderDictionary headers)
    {
        // Verificar padrões suspeitos na URL
        var suspiciousPatterns = new[]
        {
            "../", "..\\", "<script", "javascript:", "union select", "drop table",
            "exec(", "system(", "cmd=", "shell=", "eval(", "base64_decode"
        };

        if (suspiciousPatterns.Any(pattern => path.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Verificar User-Agent suspeito
        if (string.IsNullOrEmpty(userAgent) || 
            userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("crawler", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("scanner", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Verificar cabeçalhos suspeitos
        if (headers.ContainsKey("X-Forwarded-For") && headers["X-Forwarded-For"].Count > 3)
        {
            return true; // Possível tentativa de ocultação de IP
        }

        return false;
    }

    /// <summary>
    /// Obtém o endereço IP do cliente
    /// </summary>
    private static string GetClientIpAddress(HttpContext context)
    {
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            return xForwardedFor.Split(',')[0].Trim();
        }

        var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp))
        {
            return xRealIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}