using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EAM.API.Core.Models.Telemetry;

/// <summary>
/// Configurações de telemetria
/// </summary>
public class TelemetrySettings
{
    /// <summary>
    /// Nome da seção no appsettings.json
    /// </summary>
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Nome do serviço
    /// </summary>
    public string ServiceName { get; set; } = "EAM.API";

    /// <summary>
    /// Versão do serviço
    /// </summary>
    public string ServiceVersion { get; set; } = "5.0.0";

    /// <summary>
    /// Ambiente de execução
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Endpoint do Jaeger
    /// </summary>
    public string JaegerEndpoint { get; set; } = "http://localhost:14268/api/traces";

    /// <summary>
    /// Endpoint do Prometheus
    /// </summary>
    public string PrometheusEndpoint { get; set; } = "http://localhost:9090/metrics";

    /// <summary>
    /// Habilitar tracing
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Habilitar métricas
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Habilitar logging estruturado
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Taxa de amostragem para traces (0.0 a 1.0)
    /// </summary>
    public double TracingSamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Habilitar instrumentação automática
    /// </summary>
    public bool EnableAutoInstrumentation { get; set; } = true;

    /// <summary>
    /// Timeout para exportação em segundos
    /// </summary>
    public int ExportTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Intervalo de coleta de métricas em segundos
    /// </summary>
    public int MetricsCollectionIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Habilitar métricas detalhadas
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = true;

    /// <summary>
    /// Habilitar logs de performance
    /// </summary>
    public bool EnablePerformanceLogs { get; set; } = true;

    /// <summary>
    /// Habilitar trace de banco de dados
    /// </summary>
    public bool EnableDatabaseTracing { get; set; } = true;

    /// <summary>
    /// Habilitar trace de HTTP
    /// </summary>
    public bool EnableHttpTracing { get; set; } = true;

    /// <summary>
    /// Habilitar trace de cache
    /// </summary>
    public bool EnableCacheTracing { get; set; } = true;
}

/// <summary>
/// Métricas customizadas do EAM
/// </summary>
public class EamMetrics
{
    private readonly Meter _meter;

    // Contadores
    private readonly Counter<long> _eventsProcessedCounter;
    private readonly Counter<long> _screenshotsUploadedCounter;
    private readonly Counter<long> _cacheHitCounter;
    private readonly Counter<long> _cacheMissCounter;
    private readonly Counter<long> _authenticationAttemptsCounter;
    private readonly Counter<long> _authenticationFailuresCounter;
    private readonly Counter<long> _apiRequestsCounter;
    private readonly Counter<long> _apiErrorsCounter;

    // Histogramas
    private readonly Histogram<double> _requestDurationHistogram;
    private readonly Histogram<double> _eventProcessingDurationHistogram;
    private readonly Histogram<double> _databaseQueryDurationHistogram;
    private readonly Histogram<double> _cacheOperationDurationHistogram;
    private readonly Histogram<double> _screenshotUploadDurationHistogram;

    // Gauges (usando UpDownCounter)
    private readonly UpDownCounter<long> _activeSessionsGauge;
    private readonly UpDownCounter<long> _queueSizeGauge;
    private readonly UpDownCounter<long> _memoryUsageGauge;
    private readonly UpDownCounter<long> _activeAgentsGauge;

    public EamMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("EAM.API");

        // Inicializar contadores
        _eventsProcessedCounter = _meter.CreateCounter<long>(
            "eam_events_processed_total",
            "count",
            "Total number of events processed");

        _screenshotsUploadedCounter = _meter.CreateCounter<long>(
            "eam_screenshots_uploaded_total",
            "count",
            "Total number of screenshots uploaded");

        _cacheHitCounter = _meter.CreateCounter<long>(
            "eam_cache_hits_total",
            "count",
            "Total number of cache hits");

        _cacheMissCounter = _meter.CreateCounter<long>(
            "eam_cache_misses_total",
            "count",
            "Total number of cache misses");

        _authenticationAttemptsCounter = _meter.CreateCounter<long>(
            "eam_authentication_attempts_total",
            "count",
            "Total number of authentication attempts");

        _authenticationFailuresCounter = _meter.CreateCounter<long>(
            "eam_authentication_failures_total",
            "count",
            "Total number of authentication failures");

        _apiRequestsCounter = _meter.CreateCounter<long>(
            "eam_api_requests_total",
            "count",
            "Total number of API requests");

        _apiErrorsCounter = _meter.CreateCounter<long>(
            "eam_api_errors_total",
            "count",
            "Total number of API errors");

        // Inicializar histogramas
        _requestDurationHistogram = _meter.CreateHistogram<double>(
            "eam_request_duration_seconds",
            "seconds",
            "Duration of HTTP requests");

        _eventProcessingDurationHistogram = _meter.CreateHistogram<double>(
            "eam_event_processing_duration_seconds",
            "seconds",
            "Duration of event processing");

        _databaseQueryDurationHistogram = _meter.CreateHistogram<double>(
            "eam_database_query_duration_seconds",
            "seconds",
            "Duration of database queries");

        _cacheOperationDurationHistogram = _meter.CreateHistogram<double>(
            "eam_cache_operation_duration_seconds",
            "seconds",
            "Duration of cache operations");

        _screenshotUploadDurationHistogram = _meter.CreateHistogram<double>(
            "eam_screenshot_upload_duration_seconds",
            "seconds",
            "Duration of screenshot uploads");

        // Inicializar gauges
        _activeSessionsGauge = _meter.CreateUpDownCounter<long>(
            "eam_active_sessions",
            "count",
            "Number of active user sessions");

        _queueSizeGauge = _meter.CreateUpDownCounter<long>(
            "eam_queue_size",
            "count",
            "Current size of processing queue");

        _memoryUsageGauge = _meter.CreateUpDownCounter<long>(
            "eam_memory_usage_bytes",
            "bytes",
            "Current memory usage");

        _activeAgentsGauge = _meter.CreateUpDownCounter<long>(
            "eam_active_agents",
            "count",
            "Number of active agents");
    }

    // Métodos para registrar métricas
    public void RecordEventProcessed(string eventType, bool success = true)
    {
        _eventsProcessedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType),
                                       new KeyValuePair<string, object?>("success", success));
    }

    public void RecordScreenshotUploaded(string agentId, bool success = true)
    {
        _screenshotsUploadedCounter.Add(1, new KeyValuePair<string, object?>("agent_id", agentId),
                                           new KeyValuePair<string, object?>("success", success));
    }

    public void RecordCacheHit(string cacheKey)
    {
        _cacheHitCounter.Add(1, new KeyValuePair<string, object?>("cache_key", cacheKey));
    }

    public void RecordCacheMiss(string cacheKey)
    {
        _cacheMissCounter.Add(1, new KeyValuePair<string, object?>("cache_key", cacheKey));
    }

    public void RecordAuthenticationAttempt(string method, bool success = true)
    {
        _authenticationAttemptsCounter.Add(1, new KeyValuePair<string, object?>("method", method),
                                              new KeyValuePair<string, object?>("success", success));
        
        if (!success)
        {
            _authenticationFailuresCounter.Add(1, new KeyValuePair<string, object?>("method", method));
        }
    }

    public void RecordApiRequest(string method, string endpoint, int statusCode)
    {
        _apiRequestsCounter.Add(1, 
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("status_code", statusCode));

        if (statusCode >= 400)
        {
            _apiErrorsCounter.Add(1,
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("endpoint", endpoint),
                new KeyValuePair<string, object?>("status_code", statusCode));
        }
    }

    public void RecordRequestDuration(double duration, string method, string endpoint)
    {
        _requestDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("endpoint", endpoint));
    }

    public void RecordEventProcessingDuration(double duration, string eventType)
    {
        _eventProcessingDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>("event_type", eventType));
    }

    public void RecordDatabaseQueryDuration(double duration, string operation)
    {
        _databaseQueryDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordCacheOperationDuration(double duration, string operation)
    {
        _cacheOperationDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordScreenshotUploadDuration(double duration, string agentId)
    {
        _screenshotUploadDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>("agent_id", agentId));
    }

    public void SetActiveSessions(long count)
    {
        _activeSessionsGauge.Add(count - _activeSessionsGauge.GetType().GetField("_value", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_activeSessionsGauge) as long? ?? 0);
    }

    public void SetQueueSize(long size)
    {
        _queueSizeGauge.Add(size);
    }

    public void SetMemoryUsage(long bytes)
    {
        _memoryUsageGauge.Add(bytes);
    }

    public void SetActiveAgents(long count)
    {
        _activeAgentsGauge.Add(count);
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}

/// <summary>
/// Atividades de tracing personalizadas
/// </summary>
public static class EamActivitySource
{
    /// <summary>
    /// Nome da fonte de atividade
    /// </summary>
    public const string Name = "EAM.API";

    /// <summary>
    /// Versão da fonte de atividade
    /// </summary>
    public const string Version = "5.0.0";

    /// <summary>
    /// Fonte de atividade
    /// </summary>
    public static readonly ActivitySource Source = new(Name, Version);

    /// <summary>
    /// Inicia uma nova atividade
    /// </summary>
    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return Source.StartActivity(name, kind);
    }

    /// <summary>
    /// Inicia uma nova atividade com tags
    /// </summary>
    public static Activity? StartActivity(string name, ActivityKind kind, Dictionary<string, object?> tags)
    {
        var activity = Source.StartActivity(name, kind);
        if (activity != null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }
        return activity;
    }
}

/// <summary>
/// Informações de contexto da telemetria
/// </summary>
public class TelemetryContext
{
    /// <summary>
    /// ID da requisição
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// ID do usuário
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// ID do agente
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Timestamp da operação
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Endereço IP
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Contexto adicional
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();
}