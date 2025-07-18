using EAM.Agent;
using EAM.Agent.Services;
using EAM.Agent.Trackers;
using EAM.Agent.Plugins;
using EAM.Agent.Data;
using EAM.Agent.Configuration;
using EAM.Agent.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using System.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);

// Configurar como Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "EAM Agent";
});

// Configurar logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddEventLog();
});

// Configurar OpenTelemetry
var serviceName = builder.Configuration["Telemetry:ServiceName"] ?? "EAM.Agent";
var serviceVersion = builder.Configuration["Telemetry:ServiceVersion"] ?? "5.0.0";
var otlpEndpoint = builder.Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName,
            ["host.name"] = Environment.MachineName,
            ["os.type"] = "windows"
        }))
    .WithTracing(tracing => tracing
        .AddSource(serviceName)
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
        }))
    .WithMetrics(metrics => metrics
        .AddMeter(serviceName)
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
        }))
    .WithLogging(logging => logging
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
        }));

// Configurar HttpClient
builder.Services.AddHttpClient("EAMApi", client =>
{
    var baseUrl = builder.Configuration["Agent:ApiBaseUrl"] ?? "https://api.eam.local";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", $"EAM-Agent/{serviceVersion}");
});

// Registrar serviços
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<SyncService>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<PluginManager>();
builder.Services.AddSingleton<TelemetryService>();

// Registrar serviços de update
builder.Services.AddSingleton<UpdateConfig>();
builder.Services.AddSingleton<VersionManager>();
builder.Services.AddSingleton<DownloadManager>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddSingleton<FileHelper>();
builder.Services.AddSingleton<ProcessHelper>();
builder.Services.AddSingleton<SecurityHelper>();
builder.Services.AddSingleton<IUpdateService, UpdateService>();

// Registrar trackers
builder.Services.AddSingleton<WindowTracker>();
builder.Services.AddSingleton<BrowserTracker>();
builder.Services.AddSingleton<TeamsTracker>();
builder.Services.AddSingleton<ScreenshotCapturer>();
builder.Services.AddSingleton<ProcessMonitor>();

// Registrar o serviço principal
builder.Services.AddHostedService<AgentService>();

var host = builder.Build();

// Configurar Activity Source para tracing
using var activitySource = new ActivitySource(serviceName);

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Erro fatal no EAM Agent");
    throw;
}