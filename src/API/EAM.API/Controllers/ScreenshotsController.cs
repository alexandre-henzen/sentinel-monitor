using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using EAM.API.Core.Models.Requests;
using EAM.API.Core.Models.Responses;
using EAM.API.Core.Interfaces;

namespace EAM.API.Controllers;

/// <summary>
/// Controller para gerenciamento de screenshots
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ScreenshotsController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IScreenshotRepository _screenshotRepository;
    private readonly ILogger<ScreenshotsController> _logger;

    /// <summary>
    /// Construtor do controller
    /// </summary>
    /// <param name="storageService">Serviço de armazenamento</param>
    /// <param name="screenshotRepository">Repositório de screenshots</param>
    /// <param name="logger">Logger</param>
    public ScreenshotsController(
        IStorageService storageService,
        IScreenshotRepository screenshotRepository,
        ILogger<ScreenshotsController> logger)
    {
        _storageService = storageService;
        _screenshotRepository = screenshotRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gera URL pré-assinada para upload de screenshot
    /// </summary>
    /// <param name="request">Requisição para upload</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL pré-assinada</returns>
    [HttpPost("request-upload-url")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(UploadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadUrlResponse>> RequestUploadUrlAsync(
        [FromBody] UploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Gerando URL de upload para screenshot do agente {AgentId}, arquivo: {FileName}", 
                request.AgentId, request.FileName);

            // Validar requisição
            if (!IsValidFileType(request.ContentType))
            {
                return BadRequest(new ValidationProblemDetails
                {
                    Errors = { ["ContentType"] = new[] { "Tipo de arquivo não suportado" } }
                });
            }

            if (request.FileSize > 50_000_000) // 50MB
            {
                return BadRequest(new ValidationProblemDetails
                {
                    Errors = { ["FileSize"] = new[] { "Arquivo muito grande (máximo 50MB)" } }
                });
            }

            // Gerar URL de upload
            var uploadResponse = await _storageService.GenerateUploadUrlAsync(request, cancellationToken);

            _logger.LogInformation("URL de upload gerada para agente {AgentId}, ID: {UploadId}", 
                request.AgentId, uploadResponse.UploadId);

            return Ok(uploadResponse);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Dados inválidos para upload de screenshot do agente {AgentId}", request.AgentId);
            return BadRequest(new ProblemDetails
            {
                Title = "Dados inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar URL de upload para agente {AgentId}", request.AgentId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao gerar URL de upload",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Confirma o upload de um screenshot
    /// </summary>
    /// <param name="uploadId">ID do upload</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Confirmação do upload</returns>
    [HttpPost("confirm-upload/{uploadId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(UploadConfirmationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadConfirmationResponse>> ConfirmUploadAsync(
        [FromRoute] Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Confirmando upload de screenshot: {UploadId}", uploadId);

            var confirmation = await _storageService.ConfirmUploadAsync(uploadId, cancellationToken);
            
            if (!confirmation.Success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Upload não encontrado",
                    Detail = confirmation.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }

            _logger.LogInformation("Upload confirmado com sucesso: {UploadId}", uploadId);

            return Ok(confirmation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar upload: {UploadId}", uploadId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao confirmar upload",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Obtém URL de download para um screenshot
    /// </summary>
    /// <param name="screenshotId">ID do screenshot</param>
    /// <param name="expiryMinutes">Tempo de expiração em minutos (padrão: 60)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL de download</returns>
    [HttpGet("{screenshotId:guid}/download-url")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(DownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DownloadUrlResponse>> GetDownloadUrlAsync(
        [FromRoute] Guid screenshotId,
        [FromQuery] int expiryMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validar parâmetros
            if (expiryMinutes < 1 || expiryMinutes > 1440) // Entre 1 minuto e 24 horas
            {
                return BadRequest(new ValidationProblemDetails
                {
                    Errors = { ["expiryMinutes"] = new[] { "Expiração deve ser entre 1 e 1440 minutos" } }
                });
            }

            var screenshot = await _screenshotRepository.GetByIdAsync(screenshotId, cancellationToken);
            if (screenshot == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Screenshot não encontrado",
                    Detail = $"Screenshot com ID {screenshotId} não foi encontrado",
                    Status = StatusCodes.Status404NotFound
                });
            }

            if (!screenshot.IsUploaded)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Screenshot não disponível",
                    Detail = "Screenshot ainda não foi enviado para o storage",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var downloadUrl = await _storageService.GenerateDownloadUrlAsync(screenshot.ObjectKey, expiryMinutes, cancellationToken);

            var response = new DownloadUrlResponse
            {
                DownloadUrl = downloadUrl,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
                FileName = screenshot.FileName,
                FileSize = screenshot.FileSize,
                ContentType = screenshot.ContentType
            };

            _logger.LogInformation("URL de download gerada para screenshot {ScreenshotId}", screenshotId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar URL de download para screenshot {ScreenshotId}", screenshotId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao gerar URL de download",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Obtém estatísticas de screenshots
    /// </summary>
    /// <param name="startDate">Data de início (formato: yyyy-MM-dd)</param>
    /// <param name="endDate">Data de fim (formato: yyyy-MM-dd)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de screenshots</returns>
    [HttpGet("statistics")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ScreenshotStatistics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScreenshotStatistics>> GetStatisticsAsync(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validar datas
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;

            if (start >= end)
            {
                return BadRequest(new ValidationProblemDetails
                {
                    Errors = { ["dates"] = new[] { "Data de início deve ser anterior à data de fim" } }
                });
            }

            if ((end - start).TotalDays > 365)
            {
                return BadRequest(new ValidationProblemDetails
                {
                    Errors = { ["dates"] = new[] { "Período não pode ser superior a 365 dias" } }
                });
            }

            var statistics = await _screenshotRepository.GetStatisticsAsync(start, end, cancellationToken);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas de screenshots");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao obter estatísticas",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Valida se o tipo de arquivo é suportado
    /// </summary>
    /// <param name="contentType">Tipo de conteúdo</param>
    /// <returns>True se válido</returns>
    private static bool IsValidFileType(string contentType)
    {
        var supportedTypes = new[]
        {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/bmp",
            "image/gif",
            "image/webp"
        };

        return supportedTypes.Contains(contentType.ToLowerInvariant());
    }
}

/// <summary>
/// Resposta com URL de download
/// </summary>
public class DownloadUrlResponse
{
    /// <summary>
    /// URL de download
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Nome do arquivo
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Tamanho do arquivo
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Tipo de conteúdo
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
}