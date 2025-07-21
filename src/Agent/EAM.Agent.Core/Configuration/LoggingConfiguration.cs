using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EAM.Agent.Core.Configuration;

/// <summary>
/// Configuração de logging e telemetria
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configura Serilog para o aplicativo
    /// </summary>
    public static void ConfigureSerilog(IConfiguration configuration, IHostEnvironment environment)
    {
        var logConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "EAM.Agent")
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .Enrich.WithProperty("Version", "5.0.0")
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("UserName", Environment.UserName);

        // Console sink
        logConfig = logConfig.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            theme: AnsiConsoleTheme.Code);

        // File sink
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EAM", "Logs", "agent-.log");

        logConfig = logConfig.WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
            shared: true);

        // Event Log sink (apenas em produção)
        if (environment.IsProduction())
        {
            logConfig = logConfig.WriteTo.EventLog(
                source: "EAM.Agent",
                logName: "Application",
                restrictedToMinimumLevel: LogEventLevel.Warning);
        }

        // Configurações específicas por ambiente
        if (environment.IsDevelopment())
        {
            logConfig = logConfig.MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Information);
        }
        else
        {
            logConfig = logConfig.MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning);
        }

        Log.Logger = logConfig.CreateLogger();
    }

    /// <summary>
    /// Configura OpenTelemetry para telemetria
    /// </summary>
    public static void ConfigureOpenTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        var telemetryConfig = configuration.GetSection("Telemetry");
        var serviceName = "EAM.Agent";
        var serviceVersion = "5.0.0";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                    ["service.instance.id"] = Environment.MachineName,
                    ["service.namespace"] = "EAM",
                    ["host.name"] = Environment.MachineName,
                    ["os.type"] = Environment.OSVersion.Platform.ToString(),
                    ["os.version"] = Environment.OSVersion.VersionString
                }))
            .WithTracing(tracing =>
            {
                tracing.AddSource(serviceName)
                    .SetSampler(new TraceIdRatioBasedSampler(0.1));

                // Configura exporters baseado na configuração
                if (telemetryConfig.GetValue<bool>("EnableOtlp"))
                {
                    var otlpEndpoint = telemetryConfig.GetValue<string>("OtlpEndpoint");
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        tracing.AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                        });
                    }
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(serviceName);

                // Configura exporters para métricas
                if (telemetryConfig.GetValue<bool>("EnableOtlp"))
                {
                    var otlpEndpoint = telemetryConfig.GetValue<string>("OtlpEndpoint");
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        metrics.AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(otlpEndpoint);
                        });
                    }
                }
            });

        // Configuração de logs para OpenTelemetry
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(serviceName, serviceVersion));

                if (telemetryConfig.GetValue<bool>("EnableOtlp"))
                {
                    var otlpEndpoint = telemetryConfig.GetValue<string>("OtlpEndpoint");
                    if (!string.IsNullOrEmpty(otlpEndpoint))
                    {
                        options.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        });
                    }
                }
            });
        });
    }

    private static string ExtractHostFromEndpoint(string endpoint)
    {
        try
        {
            var uri = new Uri(endpoint);
            return uri.Host;
        }
        catch
        {
            return "localhost";
        }
    }

    private static int ExtractPortFromEndpoint(string endpoint)
    {
        try
        {
            var uri = new Uri(endpoint);
            return uri.Port != -1 ? uri.Port : 14268;
        }
        catch
        {
            return 14268;
        }
    }
}

/// <summary>
/// Configuração de telemetria
/// </summary>
public class TelemetryConfiguration
{
    public bool EnableTelemetry { get; set; } = true;
    public bool EnableJaeger { get; set; } = false;
    public bool EnableOtlp { get; set; } = true;
    public bool EnablePrometheus { get; set; } = false;
    public string JaegerEndpoint { get; set; } = "http://localhost:14268/api/traces";
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";
    public string PrometheusEndpoint { get; set; } = "http://localhost:9090/metrics";
    public double SamplingRatio { get; set; } = 0.1;
}