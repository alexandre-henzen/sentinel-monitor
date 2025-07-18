using EAM.Shared.Models;
using EAM.Shared.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EAM.API.Controllers;

[ApiController]
[Route(ApiEndpoints.Updates)]
public class UpdatesController : ControllerBase
{
    private readonly ILogger<UpdatesController> _logger;
    private readonly IConfiguration _configuration;

    public UpdatesController(ILogger<UpdatesController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("latest")]
    [Authorize(Policy = "Agent")]
    public async Task<IActionResult> GetLatestUpdate([FromQuery] string currentVersion = "")
    {
        try
        {
            _logger.LogInformation("Verificando updates para versão: {CurrentVersion}", currentVersion);

            // Parse da versão atual
            if (!VersionInfo.TryParse(currentVersion, out var current))
            {
                _logger.LogWarning("Versão atual inválida: {CurrentVersion}", currentVersion);
                return BadRequest(new { Error = "Versão atual inválida" });
            }

            // Simulação de versão mais recente (em produção, viria de um banco de dados ou storage)
            var latestVersion = new VersionInfo(5, 0, 1);
            
            // Verificar se há atualização disponível
            if (current >= latestVersion)
            {
                _logger.LogInformation("Nenhuma atualização disponível. Versão atual: {CurrentVersion}, Mais recente: {LatestVersion}", 
                    current, latestVersion);
                return Ok(new { UpdateAvailable = false, CurrentVersion = current.ToString() });
            }

            // Preparar informações da atualização
            var updateInfo = new UpdateInfo
            {
                Version = latestVersion.ToString(),
                DownloadUrl = GetDownloadUrl(latestVersion.ToString()),
                Checksum = GetChecksum(latestVersion.ToString()),
                ChecksumAlgorithm = "SHA256",
                FileSize = 25 * 1024 * 1024, // 25MB exemplo
                ReleaseDate = DateTime.UtcNow.AddDays(-1),
                ReleaseNotes = GetReleaseNotes(latestVersion.ToString()),
                IsRequired = IsUpdateRequired(current, latestVersion),
                IsPreRelease = latestVersion.PreRelease.Length > 0,
                MinimumVersion = "5.0.0",
                Signature = GetSignature(latestVersion.ToString()),
                SignatureAlgorithm = "SHA256withRSA",
                Metadata = new Dictionary<string, string>
                {
                    ["platform"] = "windows",
                    ["architecture"] = "x64",
                    ["installer_type"] = "msi"
                }
            };

            _logger.LogInformation("Atualização disponível: {CurrentVersion} -> {NewVersion}", 
                current, latestVersion);

            return Ok(new 
            { 
                UpdateAvailable = true, 
                UpdateInfo = updateInfo 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar atualizações");
            return StatusCode(500, new { Error = "Erro interno do servidor" });
        }
    }

    [HttpGet("check")]
    [Authorize(Policy = "Agent")]
    public async Task<IActionResult> CheckForUpdates([FromQuery] string currentVersion = "")
    {
        // Redirect para o endpoint latest para compatibilidade
        return await GetLatestUpdate(currentVersion);
    }

    [HttpGet("download/{version}")]
    [Authorize(Policy = "Agent")]
    public async Task<IActionResult> DownloadUpdate(string version)
    {
        try
        {
            if (!VersionInfo.TryParse(version, out var versionInfo))
            {
                return BadRequest(new { Error = "Versão inválida" });
            }

            var downloadUrl = GetDownloadUrl(version);
            _logger.LogInformation("Redirecionando download para: {Version} -> {Url}", version, downloadUrl);

            return Redirect(downloadUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar download da versão {Version}", version);
            return StatusCode(500, new { Error = "Erro interno do servidor" });
        }
    }

    private string GetDownloadUrl(string version)
    {
        var baseUrl = _configuration["Updates:BaseUrl"] ?? "https://updates.eam.local";
        return $"{baseUrl}/releases/v{version}/EAM.Agent.v{version}.msi";
    }

    private string GetChecksum(string version)
    {
        // Em produção, isso viria de um banco de dados ou seria calculado do arquivo
        // Por enquanto, retorna um hash exemplo
        return "sha256:a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef123456";
    }

    private string GetSignature(string version)
    {
        // Em produção, isso seria a assinatura digital real do arquivo
        return "signature:example_signature_for_verification";
    }

    private string GetReleaseNotes(string version)
    {
        return $@"EAM Agent v{version}

Melhorias:
- Correções de bugs de performance
- Melhor estabilidade do sistema de tracking
- Otimizações de memória
- Compatibilidade aprimorada com Windows 11

Correções:
- Corrigido problema com captura de screenshots
- Melhorado sistema de sincronização
- Resolvidos problemas de conectividade intermitente

Para mais detalhes, visite: https://docs.eam.local/releases/v{version}";
    }

    private bool IsUpdateRequired(VersionInfo current, VersionInfo latest)
    {
        // Atualização é obrigatória se a versão atual é muito antiga
        // ou se há problemas críticos de segurança
        var minimumSupported = new VersionInfo(4, 9, 0);
        return current < minimumSupported;
    }
}