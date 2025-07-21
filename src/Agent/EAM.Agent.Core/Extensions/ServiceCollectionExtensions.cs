using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using EAM.Agent.Core.Data;
using EAM.Agent.Core.Services;
using EAM.Agent.Core.Configuration;

namespace EAM.Agent.Core.Extensions;

/// <summary>
/// Extensões para configuração de serviços
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona todos os serviços do EAM Agent Core
    /// </summary>
    public static IServiceCollection AddEamAgentCore(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuração do banco de dados SQLite
        services.AddDbContext<AgentDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=agent.db";
            
            options.UseSqlite(connectionString);
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(configuration.GetValue<bool>("Logging:EnableDetailedErrors", false));
        });

        // Repositórios
        services.AddScoped<IActivityEventRepository, ActivityEventRepository>();

        // Configurações
        services.Configure<AgentConfiguration>(configuration.GetSection("Agent"));
        services.Configure<TrackerConfiguration>(configuration.GetSection("Trackers:WindowTracker"));
        services.Configure<TrackerConfiguration>(configuration.GetSection("Trackers:BrowserTracker"));
        services.Configure<TrackerConfiguration>(configuration.GetSection("Trackers:TeamsTracker"));
        services.Configure<ScreenshotConfiguration>(configuration.GetSection("Trackers:ScreenshotCapturer"));
        services.Configure<TrackerConfiguration>(configuration.GetSection("Trackers:ProcessMonitor"));
        services.Configure<ScoringConfiguration>(configuration.GetSection("Scoring"));
        services.Configure<TelemetryConfiguration>(configuration.GetSection("Telemetry"));

        // Trackers - usar Scoped ao invés de Singleton para resolver dependência do repository
        services.AddTransient<WindowTracker>();
        services.AddTransient<BrowserTracker>();
        services.AddTransient<TeamsTracker>();
        services.AddTransient<ScreenshotCapturer>();
        services.AddTransient<ProcessMonitor>();
        services.AddTransient<SessionTracker>(); // Novo tracker focado em sessões de uso

        // Scoring Engine
        services.AddTransient<ScoringEngine>();

        // Serviço principal
        services.AddHostedService<AgentTrackerService>();

        return services;
    }

    /// <summary>
    /// Adiciona configuração de logging com Serilog
    /// </summary>
    public static IServiceCollection AddEamLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
            builder.AddDebug();
            builder.AddEventSourceLogger();
        });

        return services;
    }

    /// <summary>
    /// Adiciona configuração de telemetria
    /// </summary>
    public static IServiceCollection AddEamTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var telemetryEnabled = configuration.GetValue<bool>("Telemetry:EnableTelemetry", true);
        
        if (telemetryEnabled)
        {
            LoggingConfiguration.ConfigureOpenTelemetry(services, configuration);
        }

        return services;
    }

}