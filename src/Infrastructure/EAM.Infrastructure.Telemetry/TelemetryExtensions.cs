using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace EAM.Infrastructure.Telemetry;

/// <summary>
/// Extensões para configuração de telemetria e observabilidade
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Configura telemetria e observabilidade
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <param name="applicationName">Nome da aplicação</param>
    /// <returns>Coleção de serviços configurada</returns>
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName)
    {
        // Configurar OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(applicationName)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["service.version"] = "1.0.0",
                            ["service.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
                        }))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddConsoleExporter();

                // Configurar exportadores baseados na configuração
                var jaegerEndpoint = configuration.GetValue<string>("OpenTelemetry:Jaeger:Endpoint");
                if (!string.IsNullOrEmpty(jaegerEndpoint))
                {
                    builder.AddJaegerExporter(options =>
                    {
                        options.Endpoint = new Uri(jaegerEndpoint);
                    });
                }
            })
            .WithMetrics(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(applicationName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter();

                // Configurar exportador Prometheus se habilitado
                if (configuration.GetValue<bool>("OpenTelemetry:Prometheus:Enabled"))
                {
                    builder.AddPrometheusExporter();
                }
            });

        return services;
    }

    /// <summary>
    /// Configura Serilog para logging estruturado
    /// </summary>
    /// <param name="hostBuilder">Builder do host</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Host builder configurado</returns>
    public static IHostBuilder UseSerilogLogging(
        this IHostBuilder hostBuilder,
        IConfiguration? configuration = null)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            var config = configuration ?? context.Configuration;
            
            loggerConfiguration
                .ReadFrom.Configuration(config)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationName", context.HostingEnvironment.ApplicationName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.Console()
                .WriteTo.File(
                    path: "logs/app-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

            // Configurar Seq se disponível
            var seqServerUrl = config.GetValue<string>("Seq:ServerUrl");
            if (!string.IsNullOrEmpty(seqServerUrl))
            {
                loggerConfiguration.WriteTo.Seq(seqServerUrl);
            }
        });
    }
}