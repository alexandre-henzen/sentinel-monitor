using EAM.Agent.Services;
using EAM.Agent.Trackers;
using EAM.Agent.Plugins;
using EAM.Agent.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace EAM.Agent;

public class AgentService : BackgroundService
{
    private readonly ILogger<AgentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly DatabaseService _databaseService;
    private readonly SyncService _syncService;
    private readonly PluginManager _pluginManager;
    private readonly TelemetryService _telemetryService;
    private readonly IUpdateService _updateService;
    private readonly UpdateConfig _updateConfig;
    
    // Trackers
    private readonly WindowTracker _windowTracker;
    private readonly BrowserTracker _browserTracker;
    private readonly TeamsTracker _teamsTracker;
    private readonly ScreenshotCapturer _screenshotCapturer;
    private readonly ProcessMonitor _processMonitor;
    
    private readonly List<Timer> _timers = new();
    private readonly ActivitySource _activitySource;

    public AgentService(
        ILogger<AgentService> logger,
        IConfiguration configuration,
        DatabaseService databaseService,
        SyncService syncService,
        PluginManager pluginManager,
        TelemetryService telemetryService,
        IUpdateService updateService,
        UpdateConfig updateConfig,
        WindowTracker windowTracker,
        BrowserTracker browserTracker,
        TeamsTracker teamsTracker,
        ScreenshotCapturer screenshotCapturer,
        ProcessMonitor processMonitor)
    {
        _logger = logger;
        _configuration = configuration;
        _databaseService = databaseService;
        _syncService = syncService;
        _pluginManager = pluginManager;
        _telemetryService = telemetryService;
        _updateService = updateService;
        _updateConfig = updateConfig;
        _windowTracker = windowTracker;
        _browserTracker = browserTracker;
        _teamsTracker = teamsTracker;
        _screenshotCapturer = screenshotCapturer;
        _processMonitor = processMonitor;
        
        _activitySource = new ActivitySource("EAM.Agent");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EAM Agent iniciando...");

        try
        {
            // Inicializar database
            await _databaseService.InitializeAsync();
            _logger.LogInformation("Database SQLite inicializado");

            // Inicializar trackers
            await InitializeTrackersAsync();
            _logger.LogInformation("Trackers inicializados");

            // Inicializar plugins
            await _pluginManager.InitializeAsync();
            _logger.LogInformation("Plugin system inicializado");

            // Configurar timers para cada tracker
            ConfigureTrackerTimers(stoppingToken);
            _logger.LogInformation("Timers configurados");

            // Aguardar até o cancelamento
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("EAM Agent parando...");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Erro fatal no EAM Agent");
            throw;
        }
        finally
        {
            await StopTrackersAsync();
            _activitySource.Dispose();
            _logger.LogInformation("EAM Agent parado");
        }
    }

    private async Task InitializeTrackersAsync()
    {
        var tasks = new List<Task>();

        if (_configuration.GetValue<bool>("Trackers:WindowTracker:Enabled"))
        {
            tasks.Add(_windowTracker.InitializeAsync());
        }

        if (_configuration.GetValue<bool>("Trackers:BrowserTracker:Enabled"))
        {
            tasks.Add(_browserTracker.InitializeAsync());
        }

        if (_configuration.GetValue<bool>("Trackers:TeamsTracker:Enabled"))
        {
            tasks.Add(_teamsTracker.InitializeAsync());
        }

        if (_configuration.GetValue<bool>("Trackers:ScreenshotCapturer:Enabled"))
        {
            tasks.Add(_screenshotCapturer.InitializeAsync());
        }

        if (_configuration.GetValue<bool>("Trackers:ProcessMonitor:Enabled"))
        {
            tasks.Add(_processMonitor.InitializeAsync());
        }

        await Task.WhenAll(tasks);
    }

    private void ConfigureTrackerTimers(CancellationToken cancellationToken)
    {
        // WindowTracker - 1 segundo
        if (_configuration.GetValue<bool>("Trackers:WindowTracker:Enabled"))
        {
            var interval = _configuration.GetValue<int>("Trackers:WindowTracker:IntervalSeconds") * 1000;
            var timer = new Timer(async _ => await ExecuteTrackerSafely(_windowTracker, "WindowTracker"), 
                null, TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));
            _timers.Add(timer);
        }

        // BrowserTracker - 2 segundos
        if (_configuration.GetValue<bool>("Trackers:BrowserTracker:Enabled"))
        {
            var interval = _configuration.GetValue<int>("Trackers:BrowserTracker:IntervalSeconds") * 1000;
            var timer = new Timer(async _ => await ExecuteTrackerSafely(_browserTracker, "BrowserTracker"), 
                null, TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));
            _timers.Add(timer);
        }

        // TeamsTracker - 5 segundos
        if (_configuration.GetValue<bool>("Trackers:TeamsTracker:Enabled"))
        {
            var interval = _configuration.GetValue<int>("Trackers:TeamsTracker:IntervalSeconds") * 1000;
            var timer = new Timer(async _ => await ExecuteTrackerSafely(_teamsTracker, "TeamsTracker"), 
                null, TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));
            _timers.Add(timer);
        }

        // ScreenshotCapturer - 60 segundos
        if (_configuration.GetValue<bool>("Trackers:ScreenshotCapturer:Enabled"))
        {
            var interval = _configuration.GetValue<int>("Trackers:ScreenshotCapturer:IntervalSeconds") * 1000;
            var timer = new Timer(async _ => await ExecuteTrackerSafely(_screenshotCapturer, "ScreenshotCapturer"), 
                null, TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));
            _timers.Add(timer);
        }

        // ProcessMonitor - 5 segundos
        if (_configuration.GetValue<bool>("Trackers:ProcessMonitor:Enabled"))
        {
            var interval = _configuration.GetValue<int>("Trackers:ProcessMonitor:IntervalSeconds") * 1000;
            var timer = new Timer(async _ => await ExecuteTrackerSafely(_processMonitor, "ProcessMonitor"), 
                null, TimeSpan.Zero, TimeSpan.FromMilliseconds(interval));
            _timers.Add(timer);
        }

        // SyncService - 60 segundos
        var syncInterval = _configuration.GetValue<int>("Agent:SyncIntervalSeconds") * 1000;
        var syncTimer = new Timer(async _ => await ExecuteSyncSafely(),
            null, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(syncInterval));
        _timers.Add(syncTimer);

        // UpdateService - verificação de atualizações
        if (_updateConfig.AutoUpdate)
        {
            var updateInterval = _updateConfig.GetNextUpdateCheckInterval();
            var updateTimer = new Timer(async _ => await ExecuteUpdateCheckSafely(),
                null, TimeSpan.FromMinutes(2), updateInterval); // Primeira verificação após 2 minutos
            _timers.Add(updateTimer);
            _logger.LogInformation("Timer de verificação de atualizações configurado: {Interval}", updateInterval);
        }
    }

    private async Task ExecuteTrackerSafely(ITracker tracker, string trackerName)
    {
        try
        {
            using var activity = _activitySource.StartActivity($"Execute{trackerName}");
            var events = await tracker.CaptureAsync();
            
            if (events.Any())
            {
                await _databaseService.SaveEventsAsync(events);
                _logger.LogDebug("{TrackerName} capturou {Count} eventos", trackerName, events.Count());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro executando {TrackerName}", trackerName);
        }
    }

    private async Task ExecuteSyncSafely()
    {
        try
        {
            using var activity = _activitySource.StartActivity("SyncWithAPI");
            await _syncService.SyncAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante sincronização");
        }
    }

    private async Task ExecuteUpdateCheckSafely()
    {
        try
        {
            using var activity = _activitySource.StartActivity("CheckForUpdates");
            await _updateService.StartUpdateProcessAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante verificação de atualizações");
        }
    }

    private async Task StopTrackersAsync()
    {
        foreach (var timer in _timers)
        {
            timer?.Dispose();
        }
        _timers.Clear();

        var tasks = new List<Task>
        {
            _windowTracker.StopAsync(),
            _browserTracker.StopAsync(),
            _teamsTracker.StopAsync(),
            _screenshotCapturer.StopAsync(),
            _processMonitor.StopAsync()
        };

        await Task.WhenAll(tasks);
    }
}