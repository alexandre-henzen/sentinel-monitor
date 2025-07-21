using EAM.API.Core.Models.Telemetry;
using EAM.Infrastructure.Telemetry.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Process;
using Serilog.Enrichers.Thread;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EAM.Infrastructure.Telemetry.Extensions;

/// <summary>
/// Extensões para configuração de telemetria
/// </summary>
public static class TelemetryServiceExtensions
{
    /// <summary>
    /// Configura telemetria e observabilidade completa
    /// </summary>
    public static IServiceCollection AddTelemetryServices(this IServiceCollection services, IConfiguration configuration)
    {
        var telemetrySettings = configuration.GetSection(TelemetrySettings.SectionName).Get<TelemetrySettings>() ?? new TelemetrySettings();
        
        // Configurar settings
        services.Configure<TelemetrySettings>(configuration.GetSection(TelemetrySettings.SectionName));
        
        // Registrar métricas customizadas
        services.AddSingleton<EamMetrics>();
        
        // Configurar OpenTelemetry
        if (telemetrySettings.EnableTracing || telemetrySettings.EnableMetrics)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(telemetrySettings.ServiceName, telemetrySettings.ServiceVersion)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["environment"] = telemetrySettings.Environment,
                        ["service.instance.id"] = Environment.MachineName,
                        ["service.namespace"] = "EAM"
                    }));
        }
        
        // Configurar tracing
        if (telemetrySettings.EnableTracing)
        {
            services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.AddSource(EamActivitySource.Name);
                    
                    if (telemetrySettings.EnableAutoInstrumentation)
                    {
                        tracing.AddAspNetCoreInstrumentation(options =>
                        {
                            options.RecordException = true;
                            options.EnrichWithHttpRequest = (activity, request) =>
                            {
                                activity.SetTag("http.request_size", request.ContentLength ?? 0);
                                activity.SetTag("http.request_id", request.HttpContext.TraceIdentifier);
                            };
                            options.EnrichWithHttpResponse = (activity, response) =>
                            {
                                activity.SetTag("http.response_size", response.ContentLength ?? 0);
                            };
                        });
                        
                        tracing.AddHttpClientInstrumentation(options =>
                        {
                            options.RecordException = true;
                            options.EnrichWithHttpRequestMessage = (activity, request) =>
                            {
                                activity.SetTag("http.request_size", request.Content?.Headers?.ContentLength ?? 0);
                            };
                            options.EnrichWithHttpResponseMessage = (activity, response) =>
                            {
                                activity.SetTag("http.response_size", response.Content?.Headers?.ContentLength ?? 0);
                            };
                        });
                    }
                    
                    if (telemetrySettings.EnableDatabaseTracing)
                    {
                        tracing.AddEntityFrameworkCoreInstrumentation(options =>
                        {
                            options.SetDbStatementForText = true;
                            options.SetDbStatementForStoredProcedure = true;
                            options.EnrichWithIDbCommand = (activity, command) =>
                            {
                                activity.SetTag("db.command_timeout", command.CommandTimeout);
                            };
                        });
                    }
                    
                    // Configurar exportadores
                    if (!string.IsNullOrEmpty(telemetrySettings.JaegerEndpoint))
                    {
                        tracing.AddJaegerExporter(options =>
                        {
                            options.Endpoint = new Uri(telemetrySettings.JaegerEndpoint);
                            options.ExportTimeout = TimeSpan.FromSeconds(telemetrySettings.ExportTimeoutSeconds);
                        });
                    }
                    
                    // Configurar sampling
                    if (telemetrySettings.TracingSamplingRate < 1.0)
                    {
                        tracing.SetSampler(new TraceIdRatioBasedSampler(telemetrySettings.TracingSamplingRate));
                    }
                });
        }
        
        // Configurar métricas
        if (telemetrySettings.EnableMetrics)
        {
            services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddMeter(EamActivitySource.Name);
                    
                    if (telemetrySettings.EnableAutoInstrumentation)
                    {
                        metrics.AddAspNetCoreInstrumentation();
                        metrics.AddHttpClientInstrumentation();
                        metrics.AddRuntimeInstrumentation();
                    }
                    
                    // Configurar exportadores
                    if (!string.IsNullOrEmpty(telemetrySettings.PrometheusEndpoint))
                    {
                        metrics.AddPrometheusExporter();
                    }
                });
        }
        
        return services;
    }
    
    /// <summary>
    /// Configura middleware de telemetria
    /// </summary>
    public static IApplicationBuilder UseTelemetryMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<TelemetryMiddleware>();
        app.UseMiddleware<PerformanceMiddleware>();
        app.UseMiddleware<SecurityTelemetryMiddleware>();
        
        return app;
    }
    
    /// <summary>
    /// Configura logging estruturado com Serilog
    /// </summary>
    public static IHostBuilder UseStructuredLogging(this IHostBuilder host, IConfiguration configuration)
    {
        var telemetrySettings = configuration.GetSection(TelemetrySettings.SectionName).Get<TelemetrySettings>() ?? new TelemetrySettings();
        
        if (telemetrySettings.EnableStructuredLogging)
        {
            host.UseSerilog((context, services, config) =>
            {
                config.ReadFrom.Configuration(configuration)
                    .ReadFrom.Services(services)
                    .Enrich.WithProperty("ServiceName", telemetrySettings.ServiceName)
                    .Enrich.WithProperty("ServiceVersion", telemetrySettings.ServiceVersion)
                    .Enrich.WithProperty("Environment", telemetrySettings.Environment)
                    .Enrich.WithMachineName()
                    .Enrich.WithProcessId()
                    .Enrich.WithProcessName()
                    .Enrich.WithThreadId()
                    .Enrich.WithThreadName()
                    .WriteTo.Console(outputTemplate: 
                        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [{ServiceName}] [{Environment}] [{RequestId}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: "logs/eam-api-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 31,
                        outputTemplate: 
                        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] [{ServiceName}] [{Environment}] [{RequestId}] {Message:lj}{NewLine}{Exception}");
            });
        }
        
        return host;
    }
    
    /// <summary>
    /// Configura endpoints de métricas
    /// </summary>
    public static IApplicationBuilder UseMetricsEndpoints(this IApplicationBuilder app)
    {
        // Endpoint para métricas do Prometheus
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPrometheusScrapingEndpoint("/metrics");
        });
        
        return app;
    }
    
    /// <summary>
    /// Configura coleta de métricas em background
    /// </summary>
    public static IServiceCollection AddMetricsCollection(this IServiceCollection services, IConfiguration configuration)
    {
        var telemetrySettings = configuration.GetSection(TelemetrySettings.SectionName).Get<TelemetrySettings>() ?? new TelemetrySettings();
        
        if (telemetrySettings.EnableMetrics)
        {
            services.AddHostedService<MetricsCollectionService>();
        }
        
        return services;
    }
}

/// <summary>
/// Serviço para coleta de métricas em background
/// </summary>
public class MetricsCollectionService : BackgroundService
{
    private readonly EamMetrics _metrics;
    private readonly ILogger<MetricsCollectionService> _logger;
    private readonly TelemetrySettings _settings;
    
    public MetricsCollectionService(
        EamMetrics metrics,
        ILogger<MetricsCollectionService> logger,
        Microsoft.Extensions.Options.IOptions<TelemetrySettings> settings)
    {
        _metrics = metrics;
        _logger = logger;
        _settings = settings.Value;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_settings.MetricsCollectionIntervalSeconds);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectSystemMetrics();
                await Task.Delay(interval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante coleta de métricas");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
    
    private async Task CollectSystemMetrics()
    {
        try
        {
            // Coletar métricas de memória
            var memoryUsage = GC.GetTotalMemory(false);
            _metrics.SetMemoryUsage(memoryUsage);
            
            // Coletar métricas de processo
            var process = Process.GetCurrentProcess();
            _metrics.SetActiveAgents(1); // Placeholder - em produção buscar do banco
            
            _logger.LogDebug("Métricas coletadas: Memória={MemoryUsage}MB", memoryUsage / 1024 / 1024);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao coletar métricas do sistema");
        }
    }
}

/// <summary>
/// Extensões para HttpContext e telemetria
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Obtém o contexto de telemetria da requisição
    /// </summary>
    public static TelemetryContext? GetTelemetryContext(this HttpContext context)
    {
        return context.Items.TryGetValue("TelemetryContext", out var telemetryContext) 
            ? telemetryContext as TelemetryContext 
            : null;
    }
    
    /// <summary>
    /// Define o contexto de telemetria da requisição
    /// </summary>
    public static void SetTelemetryContext(this HttpContext context, TelemetryContext telemetryContext)
    {
        context.Items["TelemetryContext"] = telemetryContext;
    }
    
    /// <summary>
    /// Obtém URL completa da requisição
    /// </summary>
    public static string GetDisplayUrl(this HttpRequest request)
    {
        var scheme = request.Scheme;
        var host = request.Host.Value;
        var pathBase = request.PathBase.Value;
        var path = request.Path.Value;
        var queryString = request.QueryString.Value;
        
        return $"{scheme}://{host}{pathBase}{path}{queryString}";
    }
}