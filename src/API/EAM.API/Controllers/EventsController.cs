using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using EAM.API.Core.Models.Requests;
using EAM.API.Core.Models.Responses;
using EAM.API.Core.Interfaces;

namespace EAM.API.Controllers;

/// <summary>
/// Controller para ingestão de eventos
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly IEventIngestionService _eventIngestionService;
    private readonly ILogger<EventsController> _logger;

    /// <summary>
    /// Construtor do controller
    /// </summary>
    /// <param name="eventIngestionService">Serviço de ingestão de eventos</param>
    /// <param name="logger">Logger</param>
    public EventsController(
        IEventIngestionService eventIngestionService,
        ILogger<EventsController> logger)
    {
        _eventIngestionService = eventIngestionService;
        _logger = logger;
    }

    /// <summary>
    /// Recebe lote de eventos do agente
    /// </summary>
    /// <param name="request">Requisição com lote de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado do processamento</returns>
    [HttpPost]
    [Consumes("application/x-ndjson", "application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(EventBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EventBatchResponse>> SubmitEventsAsync(
        [FromBody] EventBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Recebendo lote de eventos do agente {AgentId} com {EventCount} eventos", 
                request.AgentId, request.EventCount);

            // Validar requisição
            var validationResult = await _eventIngestionService.ValidateEventBatchAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var problemDetails = new ValidationProblemDetails();
                foreach (var error in validationResult.Errors)
                {
                    problemDetails.Errors.Add("events", new[] { error });
                }
                return BadRequest(problemDetails);
            }

            // Processar eventos
            var result = await _eventIngestionService.ProcessEventBatchAsync(request, cancellationToken);

            _logger.LogInformation("Processamento concluído para agente {AgentId}: {ProcessedEvents} processados, {RejectedEvents} rejeitados", 
                request.AgentId, result.ProcessedEvents, result.RejectedEvents);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Dados inválidos recebidos do agente {AgentId}", request.AgentId);
            return BadRequest(new ProblemDetails
            {
                Title = "Dados inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar eventos do agente {AgentId}", request.AgentId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao processar lote de eventos",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Processa eventos de forma assíncrona
    /// </summary>
    /// <param name="request">Requisição com lote de eventos</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>ID da operação assíncrona</returns>
    [HttpPost("async")]
    [Consumes("application/x-ndjson", "application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AsyncOperationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AsyncOperationResponse>> SubmitEventsAsyncAsync(
        [FromBody] EventBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Recebendo lote de eventos assíncrono do agente {AgentId} com {EventCount} eventos", 
                request.AgentId, request.EventCount);

            // Validar requisição
            var validationResult = await _eventIngestionService.ValidateEventBatchAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var problemDetails = new ValidationProblemDetails();
                foreach (var error in validationResult.Errors)
                {
                    problemDetails.Errors.Add("events", new[] { error });
                }
                return BadRequest(problemDetails);
            }

            // Processar eventos de forma assíncrona
            var operationId = await _eventIngestionService.ProcessEventBatchAsyncBackground(request, cancellationToken);

            var response = new AsyncOperationResponse
            {
                OperationId = operationId,
                Message = "Lote de eventos aceito para processamento assíncrono",
                StatusUrl = Url.Action(nameof(GetProcessingStatusAsync), new { operationId })!
            };

            _logger.LogInformation("Operação assíncrona {OperationId} iniciada para agente {AgentId}", 
                operationId, request.AgentId);

            return Accepted(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Dados inválidos recebidos do agente {AgentId}", request.AgentId);
            return BadRequest(new ProblemDetails
            {
                Title = "Dados inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar processamento assíncrono para agente {AgentId}", request.AgentId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao iniciar processamento assíncrono",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Obtém o status de processamento de uma operação assíncrona
    /// </summary>
    /// <param name="operationId">ID da operação</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Status do processamento</returns>
    [HttpGet("async/{operationId:guid}/status")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(BatchProcessingStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BatchProcessingStatus>> GetProcessingStatusAsync(
        [FromRoute] Guid operationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _eventIngestionService.GetProcessingStatusAsync(operationId, cancellationToken);
            
            if (status == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Operação não encontrada",
                    Detail = $"Operação com ID {operationId} não foi encontrada",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter status da operação {OperationId}", operationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao obter status da operação",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Obtém estatísticas de ingestão
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de ingestão</returns>
    [HttpGet("statistics")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IngestionStatistics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IngestionStatistics>> GetIngestionStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _eventIngestionService.GetIngestionStatisticsAsync(cancellationToken);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas de ingestão");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao obter estatísticas de ingestão",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}

/// <summary>
/// Resposta de operação assíncrona
/// </summary>
public class AsyncOperationResponse
{
    /// <summary>
    /// ID da operação
    /// </summary>
    public Guid OperationId { get; set; }

    /// <summary>
    /// Mensagem
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// URL para consultar o status
    /// </summary>
    public string StatusUrl { get; set; } = string.Empty;
}