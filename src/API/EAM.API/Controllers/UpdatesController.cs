using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using EAM.API.Core.Models.Responses;
using EAM.API.Core.Interfaces;

namespace EAM.API.Controllers;

/// <summary>
/// Controller para atualizações do agente
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UpdatesController : ControllerBase
{
    private readonly IUpdateService _updateService;
    private readonly ILogger<UpdatesController> _logger;

    /// <summary>
    /// Construtor do controller
    /// </summary>
    /// <param name="updateService">Serviço de atualizações</param>
    /// <param name="logger">Logger</param>
    public UpdatesController(
        IUpdateService updateService,
        ILogger<UpdatesController> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém informações da versão mais recente
    /// </summary>
    /// <param name="currentVersion">Versão atual do agente</param>
    /// <param name="osVersion">Versão do sistema operacional</param>
    /// <param name="architecture">Arquitetura do sistema</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações de atualização</returns>
    [HttpGet("latest")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(UpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UpdateResponse>> GetLatestVersionAsync(
        [FromQuery, Required] string currentVersion,
        [FromQuery] string? osVersion = null,
        [FromQuery] string? architecture = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Verificando atualizações para versão {CurrentVersion}, OS: {OsVersion}, Arch: {Architecture}", 
                currentVersion, osVersion, architecture);

            // Validar parâmetros
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                return BadRequest(new ValidationProblemDetails
                {
                    Errors = { ["currentVersion"] = new[] { "Versão atual é obrigatória" } }
                });
            }

            if (!IsValidVersion(currentVersion))
            {
                return BadRequest(new ValidationProblemDetails
                {
                    Errors = { ["currentVersion"] = new[] { "Formato de versão inválido" } }
                });
            }

            // Obter informações de atualização
            var updateResponse = await _updateService.GetLatestVersionAsync(
                currentVersion, 
                osVersion ?? "Unknown", 
                architecture ?? "Unknown", 
                cancellationToken);

            _logger.LogInformation("Verificação de atualização concluída para versão {CurrentVersion}. Última versão: {LatestVersion}, Atualização disponível: {UpdateAvailable}", 
                currentVersion, updateResponse.LatestVersion, updateResponse.UpdateAvailable);

            return Ok(updateResponse);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Parâmetros inválidos para verificação de atualização");
            return BadRequest(new ProblemDetails
            {
                Title = "Parâmetros inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar atualizações para versão {CurrentVersion}", currentVersion);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao verificar atualizações",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Obtém URL de download para uma versão específica
    /// </summary>
    /// <param name="version">Versão desejada</param>
    /// <param name="osVersion">Versão do sistema operacional</param>
    /// <param name="architecture">Arquitetura do sistema</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>URL de download</returns>
    [HttpGet("download/{version}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(DownloadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DownloadUrlResponse>> GetDownloadUrlAsync(
        [FromRoute] string version,
        [FromQuery] string? osVersion = null,
        [FromQuery] string? architecture = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Solicitando URL de download para versão {Version}, OS: {OsVersion}, Arch: {Architecture}", 
                version, osVersion, architecture);

            // Validar parâmetros
            if (!IsValidVersion(version))
            {
                return BadRequest(new ValidationProblemDetails
                {
                    Errors = { ["version"] = new[] { "Formato de versão inválido" } }
                });
            }

            // Obter URL de download
            var downloadUrl = await _updateService.GetDownloadUrlAsync(
                version, 
                osVersion ?? "Unknown", 
                architecture ?? "Unknown", 
                cancellationToken);

            if (string.IsNullOrEmpty(downloadUrl))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Versão não encontrada",
                    Detail = $"Versão {version} não está disponível para download",
                    Status = StatusCodes.Status404NotFound
                });
            }

            var response = new DownloadUrlResponse
            {
                Version = version,
                DownloadUrl = downloadUrl,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60), // URL válida por 1 hora
                GeneratedAt = DateTime.UtcNow
            };

            _logger.LogInformation("URL de download gerada para versão {Version}", version);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Parâmetros inválidos para download da versão {Version}", version);
            return BadRequest(new ProblemDetails
            {
                Title = "Parâmetros inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter URL de download para versão {Version}", version);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao obter URL de download",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Registra download de uma versão
    /// </summary>
    /// <param name="downloadRequest">Requisição de registro de download</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Confirmação do registro</returns>
    [HttpPost("register-download")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(RegisterDownloadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RegisterDownloadResponse>> RegisterDownloadAsync(
        [FromBody] RegisterDownloadRequest downloadRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Registrando download da versão {Version} para agente {AgentId}", 
                downloadRequest.Version, downloadRequest.AgentId);

            var success = await _updateService.RegisterDownloadAsync(
                downloadRequest.AgentId, 
                downloadRequest.Version, 
                cancellationToken);

            var response = new RegisterDownloadResponse
            {
                Success = success,
                Message = success ? "Download registrado com sucesso" : "Falha ao registrar download",
                RegisteredAt = success ? DateTime.UtcNow : null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar download da versão {Version} para agente {AgentId}", 
                downloadRequest.Version, downloadRequest.AgentId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao registrar download",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Registra instalação de uma versão
    /// </summary>
    /// <param name="installationRequest">Requisição de registro de instalação</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Confirmação do registro</returns>
    [HttpPost("register-installation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(RegisterInstallationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RegisterInstallationResponse>> RegisterInstallationAsync(
        [FromBody] RegisterInstallationRequest installationRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Registrando instalação da versão {Version} para agente {AgentId}: {Success}", 
                installationRequest.Version, installationRequest.AgentId, installationRequest.InstallationResult.Success);

            var success = await _updateService.RegisterInstallationAsync(
                installationRequest.AgentId, 
                installationRequest.Version, 
                installationRequest.InstallationResult, 
                cancellationToken);

            var response = new RegisterInstallationResponse
            {
                Success = success,
                Message = success ? "Instalação registrada com sucesso" : "Falha ao registrar instalação",
                RegisteredAt = success ? DateTime.UtcNow : null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar instalação da versão {Version} para agente {AgentId}", 
                installationRequest.Version, installationRequest.AgentId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao registrar instalação",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Obtém estatísticas de atualização
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de atualização</returns>
    [HttpGet("statistics")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(UpdateStatistics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UpdateStatistics>> GetUpdateStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _updateService.GetUpdateStatisticsAsync(cancellationToken);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas de atualização");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao obter estatísticas",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Obtém histórico de versões
    /// </summary>
    /// <param name="includePreRelease">Incluir versões pré-lançamento</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de versões</returns>
    [HttpGet("versions")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IEnumerable<VersionInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<VersionInfo>>> GetVersionHistoryAsync(
        [FromQuery] bool includePreRelease = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await _updateService.GetVersionHistoryAsync(includePreRelease, cancellationToken);
            return Ok(versions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter histórico de versões");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Erro interno do servidor",
                Detail = "Erro ao obter histórico de versões",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Valida se a versão tem formato válido
    /// </summary>
    /// <param name="version">Versão a ser validada</param>
    /// <returns>True se válida</returns>
    private static bool IsValidVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Aceitar formatos: 1.0.0, 1.0.0-beta, 1.0.0.0
        return System.Text.RegularExpressions.Regex.IsMatch(version, 
            @"^\d+\.\d+\.\d+(\.\d+)?(-[a-zA-Z0-9]+)?$");
    }
}

/// <summary>
/// Resposta com URL de download
/// </summary>
public class DownloadUrlResponse
{
    /// <summary>
    /// Versão
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// URL de download
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Data de expiração da URL
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Data de geração da URL
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Requisição de registro de download
/// </summary>
public class RegisterDownloadRequest
{
    /// <summary>
    /// ID do agente
    /// </summary>
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// Versão baixada
    /// </summary>
    [Required]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Resposta de registro de download
/// </summary>
public class RegisterDownloadResponse
{
    /// <summary>
    /// Sucesso
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensagem
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Data de registro
    /// </summary>
    public DateTime? RegisteredAt { get; set; }
}

/// <summary>
/// Requisição de registro de instalação
/// </summary>
public class RegisterInstallationRequest
{
    /// <summary>
    /// ID do agente
    /// </summary>
    [Required]
    public Guid AgentId { get; set; }

    /// <summary>
    /// Versão instalada
    /// </summary>
    [Required]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Resultado da instalação
    /// </summary>
    [Required]
    public InstallationResult InstallationResult { get; set; } = new();
}

/// <summary>
/// Resposta de registro de instalação
/// </summary>
public class RegisterInstallationResponse
{
    /// <summary>
    /// Sucesso
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Mensagem
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Data de registro
    /// </summary>
    public DateTime? RegisteredAt { get; set; }
}