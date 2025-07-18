using EAM.Shared.Models;
using EAM.Shared.Constants;
using EAM.Agent.Configuration;
using EAM.Agent.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace EAM.Agent.Services;

public interface IUpdateService
{
    Task<UpdateStatus> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    Task<UpdateStatus> DownloadUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default);
    Task<UpdateStatus> InstallUpdateAsync(UpdateInfo updateInfo, string installerPath, CancellationToken cancellationToken = default);
    Task<UpdateStatus> RollbackUpdateAsync(CancellationToken cancellationToken = default);
    Task<UpdateStatus> GetUpdateStatusAsync();
    Task StartUpdateProcessAsync(CancellationToken cancellationToken = default);
}

public class UpdateService : IUpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpdateConfig _updateConfig;
    private readonly VersionManager _versionManager;
    private readonly DownloadManager _downloadManager;
    private readonly BackupService _backupService;
    private readonly SecurityHelper _securityHelper;
    private readonly ProcessHelper _processHelper;
    private readonly FileHelper _fileHelper;
    private readonly ActivitySource _activitySource;

    private UpdateStatus _currentStatus = new();
    private readonly object _statusLock = new();

    public UpdateService(
        ILogger<UpdateService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        UpdateConfig updateConfig,
        VersionManager versionManager,
        DownloadManager downloadManager,
        BackupService backupService,
        SecurityHelper securityHelper,
        ProcessHelper processHelper,
        FileHelper fileHelper)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _updateConfig = updateConfig;
        _versionManager = versionManager;
        _downloadManager = downloadManager;
        _backupService = backupService;
        _securityHelper = securityHelper;
        _processHelper = processHelper;
        _fileHelper = fileHelper;
        _activitySource = new ActivitySource("EAM.Agent.UpdateService");
    }

    public async Task<UpdateStatus> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("CheckForUpdate");
        
        try
        {
            UpdateStatus("Verificando atualizações...", UpdateState.CheckingForUpdate);
            
            var currentVersion = _versionManager.GetCurrentVersion();
            _logger.LogInformation("Verificando atualizações. Versão atual: {Version}", currentVersion);

            var httpClient = _httpClientFactory.CreateClient("EAMApi");
            var response = await httpClient.GetAsync($"{ApiEndpoints.UpdatesLatest}?currentVersion={currentVersion}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = $"Erro ao verificar atualizações: {response.StatusCode}";
                _logger.LogError(error);
                UpdateStatus(error, UpdateState.Failed);
                return _currentStatus;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var updateResponse = JsonSerializer.Deserialize<UpdateResponse>(content);

            if (updateResponse?.UpdateAvailable == true && updateResponse.UpdateInfo != null)
            {
                _logger.LogInformation("Atualização disponível: {Version}", updateResponse.UpdateInfo.Version);
                
                lock (_statusLock)
                {
                    _currentStatus.State = UpdateState.UpdateAvailable;
                    _currentStatus.AvailableVersion = updateResponse.UpdateInfo.Version;
                    _currentStatus.UpdateInfo = updateResponse.UpdateInfo;
                    _currentStatus.IsUpdateRequired = updateResponse.UpdateInfo.IsRequired;
                    _currentStatus.IsPreRelease = updateResponse.UpdateInfo.IsPreRelease;
                    _currentStatus.StatusMessage = $"Atualização disponível: v{updateResponse.UpdateInfo.Version}";
                }
            }
            else
            {
                _logger.LogInformation("Nenhuma atualização disponível");
                UpdateStatus("Nenhuma atualização disponível", UpdateState.None);
            }

            _currentStatus.LastChecked = DateTime.UtcNow;
            return _currentStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar atualizações");
            UpdateStatus($"Erro ao verificar atualizações: {ex.Message}", UpdateState.Failed);
            return _currentStatus;
        }
    }

    public async Task<UpdateStatus> DownloadUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("DownloadUpdate");
        
        try
        {
            UpdateStatus("Iniciando download...", UpdateState.Downloading);
            
            var result = await _downloadManager.DownloadAsync(updateInfo, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("Download concluído: {FilePath}", result.FilePath);
                UpdateStatus("Download concluído", UpdateState.Downloaded);
                
                lock (_statusLock)
                {
                    _currentStatus.Metadata["DownloadPath"] = result.FilePath;
                }
            }
            else
            {
                _logger.LogError("Falha no download: {Error}", result.ErrorMessage);
                UpdateStatus($"Falha no download: {result.ErrorMessage}", UpdateState.Failed);
            }

            return _currentStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o download");
            UpdateStatus($"Erro durante o download: {ex.Message}", UpdateState.Failed);
            return _currentStatus;
        }
    }

    public async Task<UpdateStatus> InstallUpdateAsync(UpdateInfo updateInfo, string installerPath, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("InstallUpdate");
        
        try
        {
            // Verificar se o arquivo existe
            if (!File.Exists(installerPath))
            {
                var error = $"Arquivo de instalação não encontrado: {installerPath}";
                _logger.LogError(error);
                UpdateStatus(error, UpdateState.Failed);
                return _currentStatus;
            }

            // Verificar integridade do arquivo
            UpdateStatus("Verificando integridade do arquivo...", UpdateState.Installing);
            
            var isValid = await _securityHelper.VerifyFileIntegrityAsync(installerPath, updateInfo.Checksum, updateInfo.ChecksumAlgorithm);
            if (!isValid)
            {
                var error = "Falha na verificação de integridade do arquivo";
                _logger.LogError(error);
                UpdateStatus(error, UpdateState.Failed);
                return _currentStatus;
            }

            // Verificar assinatura digital
            if (!string.IsNullOrEmpty(updateInfo.Signature))
            {
                var isSignatureValid = await _securityHelper.VerifyDigitalSignatureAsync(installerPath, updateInfo.Signature);
                if (!isSignatureValid)
                {
                    var error = "Falha na verificação da assinatura digital";
                    _logger.LogError(error);
                    UpdateStatus(error, UpdateState.Failed);
                    return _currentStatus;
                }
            }

            // Criar backup antes da instalação
            UpdateStatus("Criando backup...", UpdateState.BackupInProgress);
            
            var backupResult = await _backupService.CreateBackupAsync(cancellationToken);
            if (!backupResult.Success)
            {
                var error = $"Falha ao criar backup: {backupResult.ErrorMessage}";
                _logger.LogError(error);
                UpdateStatus(error, UpdateState.Failed);
                return _currentStatus;
            }

            _logger.LogInformation("Backup criado: {BackupPath}", backupResult.BackupPath);
            UpdateStatus("Backup criado com sucesso", UpdateState.BackupCompleted);
            
            lock (_statusLock)
            {
                _currentStatus.Metadata["BackupPath"] = backupResult.BackupPath;
            }

            // Executar instalação silenciosa
            UpdateStatus("Instalando atualização...", UpdateState.Installing);
            
            var installResult = await _processHelper.RunMsiInstallerAsync(installerPath, "/quiet /norestart", cancellationToken);
            
            if (installResult.Success)
            {
                _logger.LogInformation("Instalação concluída com sucesso");
                UpdateStatus("Instalação concluída - Reinicialização necessária", UpdateState.RestartRequired);
                
                lock (_statusLock)
                {
                    _currentStatus.LastSuccessfulUpdate = DateTime.UtcNow;
                    _currentStatus.CurrentVersion = updateInfo.Version;
                }
            }
            else
            {
                _logger.LogError("Falha na instalação: {Error}", installResult.ErrorMessage);
                
                // Tentar rollback automático
                _logger.LogInformation("Iniciando rollback automático...");
                await RollbackUpdateAsync(cancellationToken);
            }

            return _currentStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a instalação");
            UpdateStatus($"Erro durante a instalação: {ex.Message}", UpdateState.Failed);
            
            // Tentar rollback em caso de exceção
            try
            {
                await RollbackUpdateAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Erro durante o rollback automático");
            }
            
            return _currentStatus;
        }
    }

    public async Task<UpdateStatus> RollbackUpdateAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("RollbackUpdate");
        
        try
        {
            UpdateStatus("Iniciando rollback...", UpdateState.RollingBack);
            
            var backupPath = _currentStatus.Metadata.GetValueOrDefault("BackupPath", "");
            if (string.IsNullOrEmpty(backupPath))
            {
                var error = "Caminho do backup não encontrado";
                _logger.LogError(error);
                UpdateStatus(error, UpdateState.Failed);
                return _currentStatus;
            }

            var rollbackResult = await _backupService.RestoreBackupAsync(backupPath, cancellationToken);
            
            if (rollbackResult.Success)
            {
                _logger.LogInformation("Rollback concluído com sucesso");
                UpdateStatus("Rollback concluído com sucesso", UpdateState.RolledBack);
            }
            else
            {
                _logger.LogError("Falha no rollback: {Error}", rollbackResult.ErrorMessage);
                UpdateStatus($"Falha no rollback: {rollbackResult.ErrorMessage}", UpdateState.Failed);
            }

            return _currentStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o rollback");
            UpdateStatus($"Erro durante o rollback: {ex.Message}", UpdateState.Failed);
            return _currentStatus;
        }
    }

    public async Task<UpdateStatus> GetUpdateStatusAsync()
    {
        return await Task.FromResult(_currentStatus);
    }

    public async Task StartUpdateProcessAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("StartUpdateProcess");
        
        try
        {
            // Verificar se já há um processo de update em andamento
            if (_currentStatus.IsInProgress)
            {
                _logger.LogWarning("Processo de atualização já em andamento");
                return;
            }

            // Verificar se está dentro da janela de atualização
            if (!_updateConfig.IsWithinUpdateWindow())
            {
                _logger.LogInformation("Fora da janela de atualização configurada");
                return;
            }

            // Verificar por atualizações
            var checkResult = await CheckForUpdateAsync(cancellationToken);
            if (checkResult.State != UpdateState.UpdateAvailable)
            {
                return;
            }

            var updateInfo = checkResult.UpdateInfo;
            if (updateInfo == null)
            {
                _logger.LogWarning("Informações de atualização não disponíveis");
                return;
            }

            // Decidir se deve atualizar automaticamente
            var shouldUpdate = _updateConfig.AutoUpdate || updateInfo.IsRequired;
            if (!shouldUpdate)
            {
                _logger.LogInformation("Atualização automática desabilitada e atualização não é obrigatória");
                return;
            }

            _logger.LogInformation("Iniciando processo de atualização automática para versão {Version}", updateInfo.Version);

            // Download
            var downloadResult = await DownloadUpdateAsync(updateInfo, cancellationToken);
            if (downloadResult.State != UpdateState.Downloaded)
            {
                return;
            }

            var installerPath = downloadResult.Metadata.GetValueOrDefault("DownloadPath", "");
            if (string.IsNullOrEmpty(installerPath))
            {
                _logger.LogError("Caminho do instalador não encontrado");
                return;
            }

            // Instalação
            await InstallUpdateAsync(updateInfo, installerPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o processo de atualização");
            UpdateStatus($"Erro durante o processo de atualização: {ex.Message}", UpdateState.Failed);
        }
    }

    private void UpdateStatus(string message, UpdateState state)
    {
        lock (_statusLock)
        {
            _currentStatus.StatusMessage = message;
            _currentStatus.State = state;
            _currentStatus.LastUpdateAttempt = DateTime.UtcNow;
        }
        
        _logger.LogInformation("Status de atualização: {State} - {Message}", state, message);
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
    }
}

public class UpdateResponse
{
    public bool UpdateAvailable { get; set; }
    public UpdateInfo? UpdateInfo { get; set; }
}