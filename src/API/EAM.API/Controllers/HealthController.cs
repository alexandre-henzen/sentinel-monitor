using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EAM.API.Controllers;

/// <summary>
/// Controlador de health checks
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        HealthCheckService healthCheckService,
        ILogger<HealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Verifica a saúde geral do sistema
    /// </summary>
    /// <returns>Resumo completo do health check</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 503)]
    public async Task<ActionResult> GetHealth()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            
            var result = new
            {
                status = healthReport.Status.ToString().ToLowerInvariant(),
                totalDuration = healthReport.TotalDuration.TotalMilliseconds,
                checkedAt = DateTime.UtcNow,
                entries = healthReport.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString().ToLowerInvariant(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data,
                    exception = e.Value.Exception?.Message,
                    tags = e.Value.Tags
                }),
                system = new
                {
                    serviceName = "EAM.API",
                    version = "5.0.0",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    machineName = Environment.MachineName,
                    memoryUsageMB = GC.GetTotalMemory(false) / 1024 / 1024,
                    cpuCount = Environment.ProcessorCount,
                    dotNetVersion = Environment.Version.ToString()
                }
            };

            var statusCode = healthReport.Status == HealthStatus.Healthy ? 200 : 503;
            return StatusCode(statusCode, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante verificação de saúde geral");
            
            var errorResult = new
            {
                status = "error",
                message = "Erro interno durante verificação de saúde",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            };

            return StatusCode(503, errorResult);
        }
    }

    /// <summary>
    /// Endpoint simplificado para verificação rápida
    /// </summary>
    /// <returns>Status simples OK/ERROR</returns>
    [HttpGet("simple")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 503)]
    public async Task<ActionResult> GetSimpleHealth()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            
            if (healthReport.Status == HealthStatus.Unhealthy)
            {
                return StatusCode(503, new { status = "ERROR", message = "Sistema indisponível" });
            }

            return Ok(new { status = "OK", message = "Sistema operacional", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante verificação simples de saúde");
            return StatusCode(503, new { status = "ERROR", message = "Erro interno", timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    /// Endpoint de liveness probe para Kubernetes
    /// </summary>
    /// <returns>Status da aplicação</returns>
    [HttpGet("live")]
    [ProducesResponseType(200)]
    [ProducesResponseType(503)]
    public ActionResult GetLiveness()
    {
        try
        {
            // Verificação básica de liveness - aplicação está rodando
            var memoryUsed = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            
            return Ok(new { 
                status = "alive", 
                timestamp = DateTime.UtcNow,
                memoryUsageMB = memoryUsed,
                uptime = DateTime.UtcNow.Subtract(System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante liveness probe");
            return StatusCode(503, new { status = "dead", message = "Erro interno", timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    /// Endpoint de readiness probe para Kubernetes
    /// </summary>
    /// <returns>Status de prontidão do sistema</returns>
    [HttpGet("ready")]
    [ProducesResponseType(200)]
    [ProducesResponseType(503)]
    public async Task<ActionResult> GetReadiness()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            
            // Verificar se serviços críticos estão saudáveis
            var criticalServices = healthReport.Entries.Where(e => 
                e.Value.Tags.Contains("database") || 
                e.Value.Tags.Contains("application"));

            var hasCriticalIssues = criticalServices.Any(s => s.Value.Status == HealthStatus.Unhealthy);

            if (hasCriticalIssues)
            {
                return StatusCode(503, new { 
                    status = "not ready", 
                    message = "Serviços críticos indisponíveis",
                    timestamp = DateTime.UtcNow,
                    criticalServices = criticalServices.Where(s => s.Value.Status == HealthStatus.Unhealthy)
                        .Select(s => new { name = s.Key, status = s.Value.Status.ToString() })
                });
            }

            return Ok(new { 
                status = "ready", 
                overall_status = healthReport.Status.ToString().ToLowerInvariant(),
                timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante readiness probe");
            return StatusCode(503, new { status = "not ready", message = "Erro interno", timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    /// Endpoint para métricas de saúde
    /// </summary>
    /// <returns>Métricas detalhadas do sistema</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> GetHealthMetrics()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            
            var metrics = new
            {
                timestamp = DateTime.UtcNow,
                overall_status = healthReport.Status.ToString().ToLowerInvariant(),
                total_check_time_ms = healthReport.TotalDuration.TotalMilliseconds,
                services = healthReport.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString().ToLowerInvariant(),
                    response_time_ms = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description,
                    tags = e.Value.Tags
                }),
                system = new
                {
                    service_name = "EAM.API",
                    version = "5.0.0",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    uptime_seconds = DateTime.UtcNow.Subtract(System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds,
                    memory_usage_mb = GC.GetTotalMemory(false) / 1024 / 1024,
                    cpu_count = Environment.ProcessorCount,
                    machine_name = Environment.MachineName,
                    dotnet_version = Environment.Version.ToString()
                },
                health_score = CalculateHealthScore(healthReport)
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante obtenção de métricas de saúde");
            return StatusCode(500, new { message = "Erro interno", timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    /// Verifica health check específico por nome
    /// </summary>
    /// <param name="name">Nome do health check</param>
    /// <returns>Resultado específico do health check</returns>
    [HttpGet("check/{name}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 503)]
    public async Task<ActionResult> GetHealthCheck(string name)
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync();
            
            if (!healthReport.Entries.TryGetValue(name, out var entry))
            {
                return NotFound(new { message = $"Health check '{name}' não encontrado", timestamp = DateTime.UtcNow });
            }

            var result = new
            {
                name = name,
                status = entry.Status.ToString().ToLowerInvariant(),
                description = entry.Description,
                duration = entry.Duration.TotalMilliseconds,
                data = entry.Data,
                exception = entry.Exception?.Message,
                tags = entry.Tags,
                timestamp = DateTime.UtcNow
            };

            var statusCode = entry.Status == HealthStatus.Healthy ? 200 : 503;
            return StatusCode(statusCode, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante verificação do health check {Name}", name);
            return StatusCode(503, new { message = "Erro interno", timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    /// Calcula um score de saúde baseado nos resultados
    /// </summary>
    private static double CalculateHealthScore(HealthReport report)
    {
        if (!report.Entries.Any())
            return 0.0;

        var totalServices = report.Entries.Count;
        var healthyServices = report.Entries.Count(e => e.Value.Status == HealthStatus.Healthy);
        var degradedServices = report.Entries.Count(e => e.Value.Status == HealthStatus.Degraded);
        
        // Healthy = 100%, Degraded = 50%, Unhealthy = 0%
        var score = (healthyServices * 100.0 + degradedServices * 50.0) / totalServices;
        
        return Math.Round(score, 2);
    }
}