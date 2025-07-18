using EAM.Agent.Configuration;
using EAM.Agent.Helpers;
using EAM.Agent.Services;
using EAM.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace EAM.Agent.UpdateTests;

public class UpdateServiceTests : IDisposable
{
    private readonly Mock<ILogger<UpdateService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<UpdateConfig> _updateConfigMock;
    private readonly Mock<VersionManager> _versionManagerMock;
    private readonly Mock<DownloadManager> _downloadManagerMock;
    private readonly Mock<BackupService> _backupServiceMock;
    private readonly Mock<SecurityHelper> _securityHelperMock;
    private readonly Mock<ProcessHelper> _processHelperMock;
    private readonly Mock<FileHelper> _fileHelperMock;
    private readonly Mock<HttpClient> _httpClientMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly UpdateService _updateService;

    public UpdateServiceTests()
    {
        _loggerMock = new Mock<ILogger<UpdateService>>();
        _configurationMock = new Mock<IConfiguration>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _updateConfigMock = new Mock<UpdateConfig>(_configurationMock.Object, Mock.Of<ILogger<UpdateConfig>>());
        _versionManagerMock = new Mock<VersionManager>(Mock.Of<ILogger<VersionManager>>());
        _downloadManagerMock = new Mock<DownloadManager>(
            Mock.Of<ILogger<DownloadManager>>(),
            _httpClientFactoryMock.Object,
            Mock.Of<SecurityHelper>(),
            Mock.Of<FileHelper>());
        _backupServiceMock = new Mock<BackupService>(Mock.Of<ILogger<BackupService>>());
        _securityHelperMock = new Mock<SecurityHelper>(Mock.Of<ILogger<SecurityHelper>>());
        _processHelperMock = new Mock<ProcessHelper>(Mock.Of<ILogger<ProcessHelper>>());
        _fileHelperMock = new Mock<FileHelper>(Mock.Of<ILogger<FileHelper>>());

        // Setup HTTP client mock
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientMock = new Mock<HttpClient>(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("EAMApi")).Returns(_httpClientMock.Object);

        _updateService = new UpdateService(
            _loggerMock.Object,
            _configurationMock.Object,
            _httpClientFactoryMock.Object,
            _updateConfigMock.Object,
            _versionManagerMock.Object,
            _downloadManagerMock.Object,
            _backupServiceMock.Object,
            _securityHelperMock.Object,
            _processHelperMock.Object,
            _fileHelperMock.Object);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithNoUpdateAvailable_ShouldReturnNoUpdate()
    {
        // Arrange
        var currentVersion = new VersionInfo(5, 0, 0);
        _versionManagerMock.Setup(x => x.GetCurrentVersion()).Returns(currentVersion);

        var responseContent = new
        {
            UpdateAvailable = false,
            CurrentVersion = "5.0.0"
        };

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(responseContent), Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new MockHttpMessageHandler(httpResponse));
        _httpClientFactoryMock.Setup(x => x.CreateClient("EAMApi")).Returns(httpClient);

        // Act
        var result = await _updateService.CheckForUpdateAsync();

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.None);
        result.StatusMessage.Should().Be("Nenhuma atualização disponível");
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithUpdateAvailable_ShouldReturnUpdate()
    {
        // Arrange
        var currentVersion = new VersionInfo(5, 0, 0);
        _versionManagerMock.Setup(x => x.GetCurrentVersion()).Returns(currentVersion);

        var updateInfo = new UpdateInfo
        {
            Version = "5.0.1",
            DownloadUrl = "https://updates.eam.local/v5.0.1/installer.msi",
            Checksum = "abc123",
            FileSize = 1024,
            ReleaseDate = DateTime.UtcNow,
            IsRequired = false
        };

        var responseContent = new
        {
            UpdateAvailable = true,
            UpdateInfo = updateInfo
        };

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(responseContent), Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new MockHttpMessageHandler(httpResponse));
        _httpClientFactoryMock.Setup(x => x.CreateClient("EAMApi")).Returns(httpClient);

        // Act
        var result = await _updateService.CheckForUpdateAsync();

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.UpdateAvailable);
        result.AvailableVersion.Should().Be("5.0.1");
        result.UpdateInfo.Should().NotBeNull();
        result.UpdateInfo.Version.Should().Be("5.0.1");
        result.IsUpdateRequired.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithHttpError_ShouldReturnFailure()
    {
        // Arrange
        var currentVersion = new VersionInfo(5, 0, 0);
        _versionManagerMock.Setup(x => x.GetCurrentVersion()).Returns(currentVersion);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(new MockHttpMessageHandler(httpResponse));
        _httpClientFactoryMock.Setup(x => x.CreateClient("EAMApi")).Returns(httpClient);

        // Act
        var result = await _updateService.CheckForUpdateAsync();

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.Failed);
        result.StatusMessage.Should().Contain("Erro ao verificar atualizações");
    }

    [Fact]
    public async Task DownloadUpdateAsync_WithValidUpdate_ShouldDownloadSuccessfully()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "5.0.1",
            DownloadUrl = "https://updates.eam.local/v5.0.1/installer.msi",
            Checksum = "abc123",
            FileSize = 1024
        };

        var downloadResult = new DownloadResult
        {
            Success = true,
            FilePath = @"C:\Temp\installer.msi",
            FileSize = 1024
        };

        _downloadManagerMock.Setup(x => x.DownloadAsync(updateInfo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResult);

        // Act
        var result = await _updateService.DownloadUpdateAsync(updateInfo);

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.Downloaded);
        result.StatusMessage.Should().Be("Download concluído");
        result.Metadata.Should().ContainKey("DownloadPath");
        result.Metadata["DownloadPath"].Should().Be(@"C:\Temp\installer.msi");
    }

    [Fact]
    public async Task DownloadUpdateAsync_WithDownloadFailure_ShouldReturnFailure()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "5.0.1",
            DownloadUrl = "https://updates.eam.local/v5.0.1/installer.msi",
            Checksum = "abc123",
            FileSize = 1024
        };

        var downloadResult = new DownloadResult
        {
            Success = false,
            ErrorMessage = "Network error"
        };

        _downloadManagerMock.Setup(x => x.DownloadAsync(updateInfo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResult);

        // Act
        var result = await _updateService.DownloadUpdateAsync(updateInfo);

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.Failed);
        result.StatusMessage.Should().Contain("Falha no download");
    }

    [Fact]
    public async Task InstallUpdateAsync_WithValidInstaller_ShouldInstallSuccessfully()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "5.0.1",
            Checksum = "abc123",
            ChecksumAlgorithm = "SHA256",
            Signature = "signature123"
        };

        var installerPath = @"C:\Temp\installer.msi";

        // Setup file existence check
        _fileHelperMock.Setup(x => x.GetFileSize(installerPath)).Returns(1024);

        // Setup integrity verification
        _securityHelperMock.Setup(x => x.VerifyFileIntegrityAsync(installerPath, updateInfo.Checksum, updateInfo.ChecksumAlgorithm))
            .ReturnsAsync(true);

        // Setup signature verification
        _securityHelperMock.Setup(x => x.VerifyDigitalSignatureAsync(installerPath, updateInfo.Signature))
            .ReturnsAsync(true);

        // Setup backup creation
        var backupResult = new BackupResult
        {
            Success = true,
            BackupPath = @"C:\Temp\backup.zip"
        };
        _backupServiceMock.Setup(x => x.CreateBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(backupResult);

        // Setup MSI installation
        var installResult = new ProcessResult
        {
            Success = true,
            ExitCode = 0
        };
        _processHelperMock.Setup(x => x.RunMsiInstallerAsync(installerPath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(installResult);

        // Act
        var result = await _updateService.InstallUpdateAsync(updateInfo, installerPath);

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.RestartRequired);
        result.StatusMessage.Should().Be("Instalação concluída - Reinicialização necessária");
        result.LastSuccessfulUpdate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        result.CurrentVersion.Should().Be("5.0.1");
    }

    [Fact]
    public async Task InstallUpdateAsync_WithMissingFile_ShouldReturnFailure()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "5.0.1",
            Checksum = "abc123"
        };

        var installerPath = @"C:\Temp\nonexistent.msi";

        // Setup file existence check to return false
        _fileHelperMock.Setup(x => x.GetFileSize(installerPath)).Returns(0);

        // Act
        var result = await _updateService.InstallUpdateAsync(updateInfo, installerPath);

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.Failed);
        result.StatusMessage.Should().Contain("Arquivo de instalação não encontrado");
    }

    [Fact]
    public async Task InstallUpdateAsync_WithIntegrityFailure_ShouldReturnFailure()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = "5.0.1",
            Checksum = "abc123",
            ChecksumAlgorithm = "SHA256"
        };

        var installerPath = @"C:\Temp\installer.msi";

        // Setup file existence check
        _fileHelperMock.Setup(x => x.GetFileSize(installerPath)).Returns(1024);

        // Setup integrity verification to fail
        _securityHelperMock.Setup(x => x.VerifyFileIntegrityAsync(installerPath, updateInfo.Checksum, updateInfo.ChecksumAlgorithm))
            .ReturnsAsync(false);

        // Act
        var result = await _updateService.InstallUpdateAsync(updateInfo, installerPath);

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.Failed);
        result.StatusMessage.Should().Be("Falha na verificação de integridade do arquivo");
    }

    [Fact]
    public async Task RollbackUpdateAsync_WithValidBackup_ShouldRollbackSuccessfully()
    {
        // Arrange
        var backupPath = @"C:\Temp\backup.zip";
        var updateStatus = await _updateService.GetUpdateStatusAsync();
        updateStatus.Metadata["BackupPath"] = backupPath;

        var rollbackResult = new RestoreResult
        {
            Success = true,
            FilesRestored = 10
        };

        _backupServiceMock.Setup(x => x.RestoreBackupAsync(backupPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rollbackResult);

        // Act
        var result = await _updateService.RollbackUpdateAsync();

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.RolledBack);
        result.StatusMessage.Should().Be("Rollback concluído com sucesso");
    }

    [Fact]
    public async Task RollbackUpdateAsync_WithMissingBackup_ShouldReturnFailure()
    {
        // Arrange
        // Don't set backup path in metadata

        // Act
        var result = await _updateService.RollbackUpdateAsync();

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.Failed);
        result.StatusMessage.Should().Be("Caminho do backup não encontrado");
    }

    [Fact]
    public async Task GetUpdateStatusAsync_ShouldReturnCurrentStatus()
    {
        // Act
        var result = await _updateService.GetUpdateStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(UpdateState.None);
    }

    [Fact]
    public async Task StartUpdateProcessAsync_WithUpdateAvailable_ShouldProcessUpdate()
    {
        // Arrange
        var currentVersion = new VersionInfo(5, 0, 0);
        _versionManagerMock.Setup(x => x.GetCurrentVersion()).Returns(currentVersion);

        _updateConfigMock.Setup(x => x.IsWithinUpdateWindow()).Returns(true);
        _updateConfigMock.Setup(x => x.AutoUpdate).Returns(true);

        var updateInfo = new UpdateInfo
        {
            Version = "5.0.1",
            DownloadUrl = "https://updates.eam.local/v5.0.1/installer.msi",
            Checksum = "abc123",
            FileSize = 1024,
            IsRequired = false
        };

        var responseContent = new
        {
            UpdateAvailable = true,
            UpdateInfo = updateInfo
        };

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(responseContent), Encoding.UTF8, "application/json")
        };

        var httpClient = new HttpClient(new MockHttpMessageHandler(httpResponse));
        _httpClientFactoryMock.Setup(x => x.CreateClient("EAMApi")).Returns(httpClient);

        var downloadResult = new DownloadResult
        {
            Success = true,
            FilePath = @"C:\Temp\installer.msi",
            FileSize = 1024
        };

        _downloadManagerMock.Setup(x => x.DownloadAsync(updateInfo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResult);

        // Act
        await _updateService.StartUpdateProcessAsync();

        // Assert
        var status = await _updateService.GetUpdateStatusAsync();
        status.Should().NotBeNull();
        // The exact final state depends on the full execution path
        // but it should have progressed beyond None
        status.State.Should().NotBe(UpdateState.None);
    }

    [Fact]
    public async Task StartUpdateProcessAsync_OutsideUpdateWindow_ShouldSkipUpdate()
    {
        // Arrange
        _updateConfigMock.Setup(x => x.IsWithinUpdateWindow()).Returns(false);

        // Act
        await _updateService.StartUpdateProcessAsync();

        // Assert
        var status = await _updateService.GetUpdateStatusAsync();
        status.State.Should().Be(UpdateState.None);
    }

    public void Dispose()
    {
        _httpClientMock?.Dispose();
        _updateService?.Dispose();
    }
}

// Mock HTTP message handler for testing
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public MockHttpMessageHandler(HttpResponseMessage response)
    {
        _response = response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_response);
    }
}