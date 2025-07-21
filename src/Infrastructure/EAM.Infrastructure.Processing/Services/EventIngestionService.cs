using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using System.Text.Json;
using EAM.API.Core.Configuration;
using EAM.API.Core.Entities;
using EAM.API.Core.Interfaces;
using EAM.API.Core.Models.Requests;
using EAM.Infrastructure.Processing.Models;

namespace EAM.Infrastructure.Processing.Services;

/// <summary>
/// Serviço de ingestão de eventos usando Channels para alta performance
/// </summary>
public class EventIngestionService : IEventIngestionService
{
    private readonly Channel<EventBatch> _eventChannel;
    private readonly ChannelWriter<EventBatch> _eventWriter;
    private readonly ChannelReader<EventBatch> _eventReader;
    private readonly ProcessingSettings _processingSettings;
    private readonly ILogger<EventIngestionService> _logger;
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="processingSettings">Configurações de processamento</param>
    /// <param name="logger">Logger</param>
    public EventIngestionService(
        IOptions<ProcessingSettings> processingSettings,
        ILogger<EventIngestionService> logger)
    {
        _processingSettings = processingSettings.Value;
        _logger = logger;

        // Configurar canal com capacidade limitada para controle de backpressure
        var channelOptions = new BoundedChannelOptions(_processingSettings.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _eventChannel = Channel.CreateBounded<EventBatch>(channelOptions);
        _eventWriter = _eventChannel.Writer;
        _eventReader = _eventChannel.Reader;

        _semaphore = new SemaphoreSlim(_processingSettings.MaxConcurrentBatches);
    }

    /// <summary>
    /// Processa um lote de eventos de forma assíncrona
    /// </summary>
    /// <param name="request">Requisição com lote de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado do processamento</returns>
    public async Task<EventBatchProcessingResult> ProcessEventBatchAsync(
        EventBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var batchId = Guid.NewGuid();

        try
        {
            _logger.LogDebug("Iniciando processamento de lote {BatchId} com {EventCount} eventos",
                batchId, request.Events.Count);

            // Validar lote
            var validationResult = await ValidateEventBatch(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                return new EventBatchProcessingResult
                {
                    BatchId = batchId,
                    Success = false,
                    ProcessedEvents = 0,
                    FailedEvents = request.Events.Count,
                    ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    ErrorMessage = validationResult.ErrorMessage
                };
            }

            // Criar lote interno
            var eventBatch = new EventBatch
            {
                BatchId = batchId,
                AgentId = request.AgentId,
                Events = request.Events,
                ReceivedAt = DateTime.UtcNow,
                Priority = DetermineBatchPriority(request),
                Source = request.Source ?? "API",
                RetryCount = 0,
                MaxRetries = _processingSettings.MaxRetryAttempts
            };

            // Enfileirar para processamento assíncrono
            var enqueued = await TryEnqueueBatch(eventBatch, cancellationToken);
            if (!enqueued)
            {
                return new EventBatchProcessingResult
                {
                    BatchId = batchId,
                    Success = false,
                    ProcessedEvents = 0,
                    FailedEvents = request.Events.Count,
                    ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    ErrorMessage = "Fila de processamento está cheia. Tente novamente mais tarde."
                };
            }

            // Retornar resultado imediato (processamento assíncrono)
            return new EventBatchProcessingResult
            {
                BatchId = batchId,
                Success = true,
                ProcessedEvents = request.Events.Count,
                FailedEvents = 0,
                ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                Message = "Lote enfileirado para processamento assíncrono",
                IsAsync = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar lote {BatchId}", batchId);
            
            return new EventBatchProcessingResult
            {
                BatchId = batchId,
                Success = false,
                ProcessedEvents = 0,
                FailedEvents = request.Events.Count,
                ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Obtém o leitor de eventos para processamento em background
    /// </summary>
    /// <returns>Leitor de eventos</returns>
    public ChannelReader<EventBatch> GetEventReader()
    {
        return _eventReader;
    }

    /// <summary>
    /// Obtém estatísticas de processamento
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de processamento</returns>
    public async Task<ProcessingStatistics> GetProcessingStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Placeholder para operações assíncronas futuras
        
        return new ProcessingStatistics
        {
            ChannelCapacity = _processingSettings.ChannelCapacity,
            ChannelCount = _eventChannel.Reader.Count,
            AvailableCapacity = _processingSettings.ChannelCapacity - _eventChannel.Reader.Count,
            MaxConcurrentBatches = _processingSettings.MaxConcurrentBatches,
            CurrentConcurrentBatches = _processingSettings.MaxConcurrentBatches - _semaphore.CurrentCount,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Valida um lote de eventos
    /// </summary>
    /// <param name="request">Requisição com lote de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da validação</returns>
    private async Task<BatchValidationResult> ValidateEventBatch(
        EventBatchRequest request,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder para validações assíncronas futuras

        // Validações básicas
        if (request.Events == null || request.Events.Count == 0)
        {
            return new BatchValidationResult
            {
                IsValid = false,
                ErrorMessage = "Lote de eventos não pode estar vazio"
            };
        }

        if (request.Events.Count > _processingSettings.MaxBatchSize)
        {
            return new BatchValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Lote excede o tamanho máximo de {_processingSettings.MaxBatchSize} eventos"
            };
        }

        if (request.AgentId == Guid.Empty)
        {
            return new BatchValidationResult
            {
                IsValid = false,
                ErrorMessage = "ID do agente é obrigatório"
            };
        }

        // Validar eventos individuais
        var invalidEvents = new List<string>();
        for (int i = 0; i < request.Events.Count; i++)
        {
            var eventData = request.Events[i];
            
            if (string.IsNullOrEmpty(eventData.EventType))
            {
                invalidEvents.Add($"Evento {i}: Tipo de evento é obrigatório");
            }

            if (eventData.Timestamp == default)
            {
                invalidEvents.Add($"Evento {i}: Timestamp é obrigatório");
            }

            if (eventData.Timestamp > DateTime.UtcNow.AddMinutes(5))
            {
                invalidEvents.Add($"Evento {i}: Timestamp não pode ser no futuro");
            }

            if (eventData.Timestamp < DateTime.UtcNow.AddDays(-7))
            {
                invalidEvents.Add($"Evento {i}: Timestamp muito antigo (mais de 7 dias)");
            }
        }

        if (invalidEvents.Any())
        {
            return new BatchValidationResult
            {
                IsValid = false,
                ErrorMessage = string.Join("; ", invalidEvents)
            };
        }

        return new BatchValidationResult { IsValid = true };
    }

    /// <summary>
    /// Determina a prioridade do lote baseado no conteúdo
    /// </summary>
    /// <param name="request">Requisição com lote de eventos</param>
    /// <returns>Prioridade do lote</returns>
    private BatchPriority DetermineBatchPriority(EventBatchRequest request)
    {
        // Eventos críticos têm prioridade alta
        var criticalEventTypes = new[] { "Error", "Security", "Alert", "Critical" };
        
        if (request.Events.Any(e => criticalEventTypes.Contains(e.EventType, StringComparer.OrdinalIgnoreCase)))
        {
            return BatchPriority.High;
        }

        // Eventos em tempo real têm prioridade média
        var realtimeEventTypes = new[] { "KeyboardInput", "MouseInput", "ScreenshotCaptured" };
        
        if (request.Events.Any(e => realtimeEventTypes.Contains(e.EventType, StringComparer.OrdinalIgnoreCase)))
        {
            return BatchPriority.Medium;
        }

        // Outros eventos têm prioridade baixa
        return BatchPriority.Low;
    }

    /// <summary>
    /// Tenta enfileirar um lote para processamento
    /// </summary>
    /// <param name="eventBatch">Lote de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se enfileirado com sucesso</returns>
    private async Task<bool> TryEnqueueBatch(EventBatch eventBatch, CancellationToken cancellationToken)
    {
        try
        {
            // Usar timeout para evitar bloqueio indefinido
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            await _eventWriter.WriteAsync(eventBatch, combinedCts.Token);
            
            _logger.LogDebug("Lote {BatchId} enfileirado com sucesso", eventBatch.BatchId);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout ao enfileirar lote {BatchId}", eventBatch.BatchId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enfileirar lote {BatchId}", eventBatch.BatchId);
            return false;
        }
    }

    /// <summary>
    /// Obtém informações sobre o canal de eventos
    /// </summary>
    /// <returns>Informações do canal</returns>
    public ChannelInfo GetChannelInfo()
    {
        return new ChannelInfo
        {
            Capacity = _processingSettings.ChannelCapacity,
            Count = _eventChannel.Reader.Count,
            AvailableCapacity = _processingSettings.ChannelCapacity - _eventChannel.Reader.Count,
            IsCompleted = _eventChannel.Reader.Completion.IsCompleted,
            IsFull = _eventChannel.Reader.Count >= _processingSettings.ChannelCapacity
        };
    }

    /// <summary>
    /// Sinaliza que não haverá mais eventos
    /// </summary>
    /// <returns>True se sinalização foi bem-sucedida</returns>
    public bool CompleteWriter()
    {
        try
        {
            _eventWriter.Complete();
            _logger.LogInformation("Writer do canal de eventos marcado como completo");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao marcar writer como completo");
            return false;
        }
    }

    /// <summary>
    /// Dispose dos recursos
    /// </summary>
    public void Dispose()
    {
        _semaphore?.Dispose();
        _eventWriter?.Complete();
        GC.SuppressFinalize(this);
    }
}