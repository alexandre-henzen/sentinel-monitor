using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace EAM.Shared.Logging;

/// <summary>
/// Extensions for configuring structured logging across EAM applications
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Configures Serilog with standard EAM settings
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="hostEnvironment">Host environment</param>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEAMLogging(
        this IServiceCollection services,
        IHostEnvironment hostEnvironment,
        string serviceName)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Environment", hostEnvironment.EnvironmentName)
            .Enrich.WithProperty("Version", GetAssemblyVersion())
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .CreateLogger();

        // Add Serilog to DI container
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(Log.Logger);
        });

        return services;
    }

    /// <summary>
    /// Configures Serilog with custom logger configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="logger">Custom Serilog logger</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEAMLogging(
        this IServiceCollection services,
        Serilog.ILogger logger)
    {
        Log.Logger = logger;

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(logger);
        });

        return services;
    }

    /// <summary>
    /// Gets the assembly version of the current application
    /// </summary>
    /// <returns>Assembly version string</returns>
    private static string GetAssemblyVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        return assembly?.GetName().Version?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Creates a logger for the specified type
    /// </summary>
    /// <typeparam name="T">Type to create logger for</typeparam>
    /// <returns>Logger instance</returns>
    public static Serilog.ILogger CreateLogger<T>()
    {
        return Log.ForContext<T>();
    }

    /// <summary>
    /// Creates a logger for the specified type name
    /// </summary>
    /// <param name="typeName">Type name to create logger for</param>
    /// <returns>Logger instance</returns>
    public static Serilog.ILogger CreateLogger(string typeName)
    {
        return Log.ForContext("SourceContext", typeName);
    }

    /// <summary>
    /// Logs application startup information
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="applicationName">Name of the application</param>
    /// <param name="version">Application version</param>
    public static void LogApplicationStartup(this Serilog.ILogger logger, string applicationName, string version)
    {
        logger.Information("=== {ApplicationName} v{Version} Starting ===", applicationName, version);
        logger.Information("Environment: {Environment}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown");
        logger.Information("Machine: {MachineName}", Environment.MachineName);
        logger.Information("OS: {OS}", Environment.OSVersion);
        logger.Information("Framework: {Framework}", Environment.Version);
        logger.Information("Process ID: {ProcessId}", Environment.ProcessId);
        logger.Information("Working Directory: {WorkingDirectory}", Environment.CurrentDirectory);
        logger.Information("=== Startup Complete ===");
    }

    /// <summary>
    /// Logs application shutdown information
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="applicationName">Name of the application</param>
    public static void LogApplicationShutdown(this Serilog.ILogger logger, string applicationName)
    {
        logger.Information("=== {ApplicationName} Shutting Down ===", applicationName);
        logger.Information("Uptime: {Uptime}", TimeSpan.FromMilliseconds(Environment.TickCount64));
        logger.Information("=== Shutdown Complete ===");
    }

    /// <summary>
    /// Logs configuration information
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">Configuration to log</param>
    public static void LogConfiguration(this Serilog.ILogger logger, object configuration)
    {
        logger.Information("Configuration: {@Configuration}", configuration);
    }

    /// <summary>
    /// Logs performance metrics
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="duration">Duration of the operation</param>
    /// <param name="success">Whether the operation was successful</param>
    public static void LogPerformance(this Serilog.ILogger logger, string operationName, TimeSpan duration, bool success = true)
    {
        logger.Information("Performance: {OperationName} completed in {Duration}ms with {Status}",
            operationName, duration.TotalMilliseconds, success ? "Success" : "Failure");
    }

    /// <summary>
    /// Logs security-related events
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="eventType">Type of security event</param>
    /// <param name="userId">User ID involved in the event</param>
    /// <param name="details">Additional details about the event</param>
    public static void LogSecurityEvent(this Serilog.ILogger logger, string eventType, string userId, string details)
    {
        logger.Warning("Security Event: {EventType} for User {UserId} - {Details}", eventType, userId, details);
    }

    /// <summary>
    /// Logs business events
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="eventName">Name of the business event</param>
    /// <param name="data">Event data</param>
    public static void LogBusinessEvent(this Serilog.ILogger logger, string eventName, object data)
    {
        logger.Information("Business Event: {EventName} - {@Data}", eventName, data);
    }

    /// <summary>
    /// Logs error with correlation ID
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="exception">Exception to log</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    /// <param name="message">Error message</param>
    public static void LogError(this Serilog.ILogger logger, Exception exception, string correlationId, string message)
    {
        logger.Error(exception, "{Message} - CorrelationId: {CorrelationId}", message, correlationId);
    }

    /// <summary>
    /// Logs structured data with correlation ID
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    /// <param name="level">Log level</param>
    /// <param name="message">Log message</param>
    /// <param name="data">Structured data to log</param>
    public static void LogWithCorrelation(this Serilog.ILogger logger, string correlationId, LogEventLevel level, string message, object data)
    {
        logger.Write(level, "{Message} - CorrelationId: {CorrelationId} - {@Data}", message, correlationId, data);
    }
}