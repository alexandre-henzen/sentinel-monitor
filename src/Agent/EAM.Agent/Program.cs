using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace EAM.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Iniciando EAM Agent v{Version}", GetVersion());

        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "EAM Agent falhou ao iniciar");
            throw;
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "EAM Agent";
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                    optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables("EAM_");
                config.AddUserSecrets<Program>(optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Core Services - Para ser implementado
                services.AddHostedService<AgentBackgroundService>();

                // HTTP Client
                services.AddHttpClient("EAM.API", client =>
                {
                    client.BaseAddress = new Uri(context.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7001");
                    client.DefaultRequestHeaders.Add("User-Agent", $"EAM-Agent/{GetVersion()}");
                });

                // Configuration - Para ser implementado
                // services.Configure<AgentSettings>(context.Configuration.GetSection("AgentSettings"));
                
                // Configure OpenTelemetry
                var telemetrySettings = context.Configuration.GetSection("Telemetry");
                var serviceName = telemetrySettings["ServiceName"] ?? "EAM.Agent";
                var serviceVersion = telemetrySettings["ServiceVersion"] ?? GetVersion();
                var enableTracing = telemetrySettings.GetValue<bool>("EnableTracing", true);
                var enableMetrics = telemetrySettings.GetValue<bool>("EnableMetrics", true);

                if (enableTracing || enableMetrics)
                {
                    services.AddOpenTelemetry()
                        .ConfigureResource(resource => resource.AddService(
                            serviceName: serviceName,
                            serviceVersion: serviceVersion,
                            serviceInstanceId: Environment.MachineName)
                            .AddAttributes(new Dictionary<string, object>
                            {
                                {"service.environment", context.HostingEnvironment.EnvironmentName},
                                {"service.instance.id", Environment.MachineName},
                                {"host.name", Environment.MachineName},
                                {"process.pid", Environment.ProcessId}
                            }))
                        .WithTracing(tracing =>
                        {
                            if (enableTracing)
                            {
                                tracing.AddSource(serviceName);
                                tracing.AddHttpClientInstrumentation(options =>
                                {
                                    options.RecordException = true;
                                });
                                
                                // Configurar Jaeger se endpoint estiver configurado
                                var jaegerEndpoint = telemetrySettings["JaegerEndpoint"];
                                if (!string.IsNullOrEmpty(jaegerEndpoint))
                                {
                                    tracing.AddJaegerExporter(options =>
                                    {
                                        options.Endpoint = new Uri(jaegerEndpoint);
                                    });
                                }
                            }
                        })
                        .WithMetrics(metrics =>
                        {
                            if (enableMetrics)
                            {
                                metrics.AddMeter(serviceName);
                                metrics.AddMeter("EAM.Agent.Metrics");
                                metrics.AddHttpClientInstrumentation();
                                metrics.AddRuntimeInstrumentation();
                                
                                // Configurar Prometheus se endpoint estiver configurado
                                var prometheusEndpoint = telemetrySettings["PrometheusEndpoint"];
                                if (!string.IsNullOrEmpty(prometheusEndpoint))
                                {
                                    metrics.AddPrometheusExporter();
                                }
                            }
                        });
                }
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddEventLog();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            });

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        return version ?? "1.0.0.0";
    }
}