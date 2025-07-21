using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using EAM.API.Core.Entities;
using EAM.Infrastructure.Processing.Models;

namespace EAM.Infrastructure.Processing.Services;

/// <summary>
/// Serviço de processamento de eventos em background
/// </summary>
public class EventProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventIngestionService _eventIngestionService;
    private readonly ProcessingSettings _processingSettings;
    private readonly ILogger<EventProcessingService> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly CancellationTokenSource _stoppingCts = new();

    // Estatísticas de processamento
    private long _totalBatchesProcessed = 0;
    private long _totalEventsProcessed = 0;
    private long _totalFailedBatches = 0;
    private long _totalFailedEvents = 0;
    private readonly List<double> _processingTimes = new();
    private readonly object _statsLock = new();

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="serviceProvider">Provedor de serviços</param>
    /// <param name="eventIngestionService">Serviço de ingestão</param>
    /// <param name="processingSettings">Configurações de processamento</param>
    /// <param name="logger">Logger</param>
    public EventProcessingService(
        IServiceProvider serviceProvider,
        IEventIngestionService eventIngestionService,
        IOptions<ProcessingSettings> processingSettings,
        ILogger<EventProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _eventIngestionService = eventIngestionService;
        _processingSettings = processingSettings.Value;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_processingSettings.MaxConcurrentBatches);
    }

    /// <summary>
    /// Executa o processamento em background
    /// </summary>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Serviço de processamento de eventos iniciado");

        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, _stoppingCts.Token).Token;

        try
        {
            var eventReader = _eventIngestionService.GetEventReader();
            var tasks = new List<Task>();

            // Processar eventos até ser cancelado
            await foreach (var eventBatch in eventReader.ReadAllAsync(combinedToken))
            {
                // Aguardar por um slot disponível
                await _semaphore.WaitAsync(combinedToken);

                // Processar lote em paralelo
                var processingTask = ProcessEventBatchAsync(eventBatch, combinedToken);
                tasks.Add(processingTask);

                // Limpar tasks completadas
                tasks.RemoveAll(t => t.IsCompleted);

                // Limitar número de tasks simultâneas
                if (tasks.Count >= _processingSettings.MaxConcurrentBatches)
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCompleted);
                }
            }

            // Aguardar todas as tasks restantes
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            _logger.LogInformation("Processamento de eventos concluído. Total processado: {TotalBatches} lotes, {TotalEvents} eventos",
                _totalBatchesProcessed, _totalEventsProcessed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processamento de eventos cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro crítico no processamento de eventos");
        }
        finally
        {
            _logger.LogInformation("Serviço de processamento de eventos finalizado");
        }
    }

    /// <summary>
    /// Processa um lote de eventos
    /// </summary>
    /// <param name="eventBatch">Lote de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task ProcessEventBatchAsync(EventBatch eventBatch, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var processedEvents = 0;
        var failedEvents = 0;

        try
        {
            _logger.LogDebug("Iniciando processamento do lote {BatchId} com {EventCount} eventos",
                eventBatch.BatchId, eventBatch.Size);

            using var scope = _serviceProvider.CreateScope();
            var activityEventRepository = scope.ServiceProvider.GetRequiredService<IActivityEventRepository>();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // Converter eventos para entidades
            var activityEvents = ConvertToActivityEvents(eventBatch);

            // Processar eventos em batches menores para otimizar performance
            var batchSize = _processingSettings.DatabaseBatchSize;
            var eventBatches = activityEvents.Chunk(batchSize);

            foreach (var batch in eventBatches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Inserir eventos no banco de dados
                    await activityEventRepository.BulkInsertAsync(batch, cancellationToken);
                    
                    // Atualizar cache com estatísticas
                    await UpdateCacheStatistics(eventBatch.AgentId, batch.Length, cacheService, cancellationToken);
                    
                    processedEvents += batch.Length;
                    
                    _logger.LogDebug("Processado sub-lote de {BatchSize} eventos do lote {BatchId}",
                        batch.Length, eventBatch.BatchId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar sub-lote do lote {BatchId}", eventBatch.BatchId);
                    failedEvents += batch.Length;
                    
                    // Se não é um erro crítico, continuar com o próximo sub-lote
                    if (!IsCriticalError(ex))
                    {
                        continue;
                    }
                    
                    throw;
                }
            }

            // Atualizar estatísticas globais
            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            UpdateProcessingStatistics(1, processedEvents, 0, failedEvents, processingTime);

            _logger.LogInformation("Lote {BatchId} processado com sucesso: {ProcessedEvents} eventos processados, {FailedEvents} falharam em {ProcessingTime:F2}ms",
                eventBatch.BatchId, processedEvents, failedEvents, processingTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar lote {BatchId}", eventBatch.BatchId);
            
            // Tentar reprocessar se possível
            if (eventBatch.CanRetry)
            {
                await HandleRetry(eventBatch, ex, cancellationToken);
            }
            else
            {
                _logger.LogError("Lote {BatchId} descartado após {RetryCount} tentativas", 
                    eventBatch.BatchId, eventBatch.RetryCount);
            }

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            UpdateProcessingStatistics(0, 0, 1, eventBatch.Size, processingTime);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Converte eventos da requisição para entidades
    /// </summary>
    /// <param name="eventBatch">Lote de eventos</param>
    /// <returns>Lista de entidades de eventos</returns>
    private List<ActivityEvent> ConvertToActivityEvents(EventBatch eventBatch)
    {
        var activityEvents = new List<ActivityEvent>();

        foreach (var eventData in eventBatch.Events)
        {
            var activityEvent = new ActivityEvent
            {
                Id = Guid.NewGuid(),
                AgentId = eventBatch.AgentId,
                EventType = eventData.EventType,
                Timestamp = eventData.Timestamp,
                Duration = eventData.Duration,
                ApplicationName = eventData.ApplicationName,
                WindowTitle = eventData.WindowTitle,
                ProcessName = eventData.ProcessName,
                KeyboardInput = eventData.KeyboardInput,
                MouseClicks = eventData.MouseClicks,
                MouseMovement = eventData.MouseMovement,
                ScrollActivity = eventData.ScrollActivity,
                IsActive = eventData.IsActive,
                IsIdle = eventData.IsIdle,
                IdleTime = eventData.IdleTime,
                ScreenResolution = eventData.ScreenResolution,
                CreatedAt = DateTime.UtcNow
            };

            // Adicionar dados extras como JSON
            if (eventData.AdditionalData?.Count > 0)
            {
                activityEvent.AdditionalData = System.Text.Json.JsonSerializer.Serialize(eventData.AdditionalData);
            }

            activityEvents.Add(activityEvent);
        }

        return activityEvents;
    }

    /// <summary>
    /// Atualiza estatísticas no cache
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="eventCount">Número de eventos</param>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task UpdateCacheStatistics(Guid agentId, int eventCount, ICacheService cacheService, CancellationToken cancellationToken)
    {
        try
        {
            // Incrementar contadores no cache
            await cacheService.IncrementAsync($"metrics:events:total", eventCount, cancellationToken);
            await cacheService.IncrementAsync($"metrics:events:agent:{agentId}", eventCount, cancellationToken);
            await cacheService.IncrementAsync($"metrics:events:daily:{DateTime.UtcNow:yyyy-MM-dd}", eventCount, cancellationToken);
            
            // Atualizar timestamp da última atividade do agente
            await cacheService.SetAsync($"agent:{agentId}:last-activity", DateTime.UtcNow, TimeSpan.FromHours(1), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao atualizar estatísticas no cache para agente {AgentId}", agentId);
        }
    }

    /// <summary>
    /// Manipula tentativas de reprocessamento
    /// </summary>
    /// <param name="eventBatch">Lote de eventos</param>
    /// <param name="exception">Exceção ocorrida</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task HandleRetry(EventBatch eventBatch, Exception exception, CancellationToken cancellationToken)
    {
        eventBatch.RetryCount++;
        eventBatch.LastProcessingAttempt = DateTime.UtcNow;
        eventBatch.LastError = exception.Message;

        var delay = TimeSpan.FromSeconds(Math.Pow(2, eventBatch.RetryCount) * _processingSettings.RetryDelaySeconds);
        
        _logger.LogWarning("Reprocessando lote {BatchId} em {Delay}s (tentativa {RetryCount}/{MaxRetries})",
            eventBatch.BatchId, delay.TotalSeconds, eventBatch.RetryCount, eventBatch.MaxRetries);

        await Task.Delay(delay, cancellationToken);
        
        // Reenviar para a fila (implementação simplificada)
        // Em uma implementação real, seria necessário um mecanismo mais robusto
        await ProcessEventBatchAsync(eventBatch, cancellationToken);
    }

    /// <summary>
    /// Verifica se é um erro crítico
    /// </summary>
    /// <param name="exception">Exceção</param>
    /// <returns>True se é erro crítico</returns>
    private static bool IsCriticalError(Exception exception)
    {
        return exception is OutOfMemoryException ||
               exception is StackOverflowException ||
               exception is AccessViolationException;
    }

    /// <summary>
    /// Atualiza estatísticas de processamento
    /// </summary>
    /// <param name="successfulBatches">Lotes bem-sucedidos</param>
    /// <param name="successfulEvents">Eventos bem-sucedidos</param>
    /// <param name="failedBatches">Lotes com falha</param>
    /// <param name="failedEvents">Eventos com falha</param>
    /// <param name="processingTimeMs">Tempo de processamento</param>
    private void UpdateProcessingStatistics(int successfulBatches, int successfulEvents, int failedBatches, int failedEvents, double processingTimeMs)
    {
        lock (_statsLock)
        {
            _totalBatchesProcessed += successfulBatches;
            _totalEventsProcessed += successfulEvents;
            _totalFailedBatches += failedBatches;
            _totalFailedEvents += failedEvents;
            
            _processingTimes.Add(processingTimeMs);
            
            // Manter apenas os últimos 1000 tempos para cálculo de média
            if (_processingTimes.Count > 1000)
            {
                _processingTimes.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Obtém estatísticas de processamento
    /// </summary>
    /// <returns>Estatísticas atuais</returns>
    public ProcessingStatistics GetProcessingStatistics()
    {
        lock (_statsLock)
        {
            var totalBatches = _totalBatchesProcessed + _totalFailedBatches;
            var totalEvents = _totalEventsProcessed + _totalFailedEvents;
            
            return new ProcessingStatistics
            {
                TotalBatchesProcessed = _totalBatchesProcessed,
                TotalEventsProcessed = _totalEventsProcessed,
                TotalFailedBatches = _totalFailedBatches,
                TotalFailedEvents = _totalFailedEvents,
                AverageProcessingTimeMs = _processingTimes.Count > 0 ? _processingTimes.Average() : 0,
                SuccessRate = totalBatches > 0 ? (double)_totalBatchesProcessed / totalBatches : 0,
                ProcessingRate = CalculateProcessingRate(),
                MaxConcurrentBatches = _processingSettings.MaxConcurrentBatches,
                CurrentConcurrentBatches = _processingSettings.MaxConcurrentBatches - _semaphore.CurrentCount,
                Status = DetermineProcessingStatus(),
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Calcula taxa de processamento
    /// </summary>
    /// <returns>Taxa de processamento (lotes/segundo)</returns>
    private double CalculateProcessingRate()
    {
        if (_processingTimes.Count == 0) return 0;
        
        var avgTimeMs = _processingTimes.Average();
        return avgTimeMs > 0 ? 1000.0 / avgTimeMs : 0;
    }

    /// <summary>
    /// Determina status do processamento
    /// </summary>
    /// <returns>Status atual</returns>
    private ProcessingStatus DetermineProcessingStatus()
    {
        var statistics = _eventIngestionService.GetProcessingStatisticsAsync().GetAwaiter().GetResult();
        
        if (statistics.ChannelUtilization > 0.9)
            return ProcessingStatus.Critical;
        
        if (statistics.ChannelUtilization > 0.7)
            return ProcessingStatus.Warning;
        
        if (statistics.ConcurrencyUtilization > 0.8)
            return ProcessingStatus.Degraded;
        
        return ProcessingStatus.Healthy;
    }

    /// <summary>
    /// Para o processamento graciosamente
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Parando serviço de processamento de eventos...");
        
        _stoppingCts.Cancel();
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("Serviço de processamento de eventos parado");
    }

    /// <summary>
    /// Dispose dos recursos
    /// </summary>
    public override void Dispose()
    {
        _semaphore?.Dispose();
        _stoppingCts?.Dispose();
        base.Dispose();
    }
}