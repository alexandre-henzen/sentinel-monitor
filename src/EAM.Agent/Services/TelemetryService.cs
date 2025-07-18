using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EAM.Agent.Services;

public class TelemetryService
{
    private readonly ILogger<TelemetryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    
    // Métricas
    private readonly Counter<long> _eventsProcessedCounter;
    private readonly Counter<long> _eventsSyncedCounter;
    private readonly Counter<long> _syncErrorsCounter;
    private readonly Histogram<double> _syncDurationHistogram;
    private readonly Gauge<int> _unsyncedEventsGauge;
    private readonly Counter<long> _screenshotsCapturedCounter;
    private readonly Histogram<double> _screenshotCaptureTimeHistogram;

    public TelemetryService(ILogger<TelemetryService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        var serviceName = _configuration["Telemetry:ServiceName"] ?? "EAM.Agent";
        var serviceVersion = _configuration["Telemetry:ServiceVersion"] ?? "5.0.0";
        
        _activitySource = new ActivitySource(serviceName, serviceVersion);
        _meter = new Meter(serviceName, serviceVersion);
        
        // Inicializar métricas
        _eventsProcessedCounter = _meter.CreateCounter<long>(
            "eam_events_processed_total",
            "events",
            "Total number of events processed by trackers");
        
        _eventsSyncedCounter = _meter.CreateCounter<long>(
            "eam_events_synced_total", 
            "events",
            "Total number of events successfully synced to API");
        
        _syncErrorsCounter = _meter.CreateCounter<long>(
            "eam_sync_errors_total",
            "errors", 
            "Total number of sync errors");
        
        _syncDurationHistogram = _meter.CreateHistogram<double>(
            "eam_sync_duration_seconds",
            "seconds",
            "Time spent syncing events to API");
        
        _unsyncedEventsGauge = _meter.CreateGauge<int>(
            "eam_unsynced_events",
            "events",
            "Number of events waiting to be synced");
        
        _screenshotsCapturedCounter = _meter.CreateCounter<long>(
            "eam_screenshots_captured_total",
            "screenshots",
            "Total number of screenshots captured");
        
        _screenshotCaptureTimeHistogram = _meter.CreateHistogram<double>(
            "eam_screenshot_capture_duration_seconds",
            "seconds",
            "Time spent capturing screenshots");
    }

    public Activity? StartActivity(string name)
    {
        return _activitySource.StartActivity(name);
    }

    public void RecordEventsProcessed(string trackerName, int count)
    {
        _eventsProcessedCounter.Add(count, new KeyValuePair<string, object?>("tracker", trackerName));
        _logger.LogDebug("Events processed: {TrackerName} = {Count}", trackerName, count);
    }

    public void RecordEventsSynced(int count)
    {
        _eventsSyncedCounter.Add(count);
        _logger.LogDebug("Events synced: {Count}", count);
    }

    public void RecordSyncError(string errorType)
    {
        _syncErrorsCounter.Add(1, new KeyValuePair<string, object?>("error_type", errorType));
        _logger.LogDebug("Sync error recorded: {ErrorType}", errorType);
    }

    public void RecordSyncDuration(double durationSeconds)
    {
        _syncDurationHistogram.Record(durationSeconds);
        _logger.LogDebug("Sync duration recorded: {Duration}s", durationSeconds);
    }

    public void RecordUnsyncedEvents(int count)
    {
        _unsyncedEventsGauge.Record(count);
        _logger.LogDebug("Unsynced events: {Count}", count);
    }

    public void RecordScreenshotCaptured(double captureDurationSeconds)
    {
        _screenshotsCapturedCounter.Add(1);
        _screenshotCaptureTimeHistogram.Record(captureDurationSeconds);
        _logger.LogDebug("Screenshot captured in {Duration}s", captureDurationSeconds);
    }

    public void RecordTrackerError(string trackerName, string errorType)
    {
        using var activity = _activitySource.StartActivity($"TrackerError.{trackerName}");
        activity?.SetTag("tracker.name", trackerName);
        activity?.SetTag("error.type", errorType);
        activity?.SetStatus(ActivityStatusCode.Error, $"Tracker error: {errorType}");
        
        _logger.LogDebug("Tracker error recorded: {TrackerName} - {ErrorType}", trackerName, errorType);
    }

    public void RecordPluginEvent(string pluginName, string eventType)
    {
        using var activity = _activitySource.StartActivity($"PluginEvent.{pluginName}");
        activity?.SetTag("plugin.name", pluginName);
        activity?.SetTag("event.type", eventType);
        
        _logger.LogDebug("Plugin event recorded: {PluginName} - {EventType}", pluginName, eventType);
    }

    public void RecordSystemMetrics()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            
            // Métricas do processo
            RecordProcessMetrics(process);
            
            // Métricas do sistema
            RecordSystemResourceMetrics();
            
            _logger.LogDebug("System metrics recorded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording system metrics");
        }
    }

    private void RecordProcessMetrics(Process process)
    {
        try
        {
            var memoryUsage = process.WorkingSet64 / 1024.0 / 1024.0; // MB
            var cpuTime = process.TotalProcessorTime.TotalSeconds;
            var threadCount = process.Threads.Count;
            var handleCount = process.HandleCount;

            using var activity = _activitySource.StartActivity("SystemMetrics");
            activity?.SetTag("process.memory_mb", memoryUsage);
            activity?.SetTag("process.cpu_time_seconds", cpuTime);
            activity?.SetTag("process.thread_count", threadCount);
            activity?.SetTag("process.handle_count", handleCount);
            
            _logger.LogDebug("Process metrics - Memory: {Memory:F2}MB, CPU: {CPU:F2}s, Threads: {Threads}, Handles: {Handles}",
                memoryUsage, cpuTime, threadCount, handleCount);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error recording process metrics");
        }
    }

    private void RecordSystemResourceMetrics()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false) / 1024.0 / 1024.0; // MB
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);

            using var activity = _activitySource.StartActivity("SystemResources");
            activity?.SetTag("gc.total_memory_mb", totalMemory);
            activity?.SetTag("gc.gen0_collections", gen0Collections);
            activity?.SetTag("gc.gen1_collections", gen1Collections);
            activity?.SetTag("gc.gen2_collections", gen2Collections);
            
            _logger.LogDebug("GC metrics - Memory: {Memory:F2}MB, Gen0: {Gen0}, Gen1: {Gen1}, Gen2: {Gen2}",
                totalMemory, gen0Collections, gen1Collections, gen2Collections);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error recording system resource metrics");
        }
    }

    public void RecordHealthCheck(string component, bool isHealthy, double responseTimeMs)
    {
        using var activity = _activitySource.StartActivity($"HealthCheck.{component}");
        activity?.SetTag("component", component);
        activity?.SetTag("is_healthy", isHealthy);
        activity?.SetTag("response_time_ms", responseTimeMs);
        
        if (!isHealthy)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Health check failed");
        }
        
        _logger.LogDebug("Health check recorded: {Component} - {Status} ({ResponseTime}ms)", 
            component, isHealthy ? "Healthy" : "Unhealthy", responseTimeMs);
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
        _meter?.Dispose();
    }
}