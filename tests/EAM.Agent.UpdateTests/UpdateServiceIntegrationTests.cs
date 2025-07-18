using EAM.Agent.Configuration;
using EAM.Agent.Services;
using EAM.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace EAM.Agent.UpdateTests;

public class UpdateServiceIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly UpdateService _updateService;
    private readonly string _tempDirectory;
    private readonly string _tempDownloadPath;
    private readonly string _tempBackupPath;

    public UpdateServiceIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EAM.UpdateTests", Guid.NewGuid().ToString());
        _tempDownloadPath = Path.Combine(_tempDirectory, "downloads");
        _tempBackupPath = Path.Combine(_tempDirectory, "backups");
        
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_tempDownloadPath);
        Directory.CreateDirectory(_tempBackupPath);

        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<UpdateConfig>(provider => new UpdateConfig
        {
            IsEnabled = true,
            CheckIntervalMinutes = 60,
            DownloadTimeout = TimeSpan.FromMinutes(30),
            MaxRetries = 3,
            BackupEnabled = true,
            AutoInstall = true,
            DownloadPath = _tempDownloadPath,
            BackupPath = _tempBackupPath,
            ApiBaseUrl = "https://api.example.com",
            VerifySignatures = true,
            TrustedPublishers = new[] { "Test Publisher" }
        });
        
        services.AddSingleton<IVersionManager, VersionManager>();
        services.AddSingleton<IDownloadManager, DownloadManager>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<UpdateService>();
        
        services.AddHttpClient<UpdateService>(client =>
        {
            client.BaseAddress = new Uri("https://api.example.com");
            client.Timeout = TimeSpan.FromMinutes(5);
        }).ConfigurePrimaryHttpMessageHandler(() => _mockHttpMessageHandler.Object);

        _serviceProvider = services.BuildServiceProvider();
        _updateService = _serviceProvider.GetRequiredService<UpdateService>();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenUpdateAvailable_ShouldReturnUpdateInfo()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            ReleaseDate = DateTimeOffset.UtcNow,
            DownloadUrl = "https://api.example.com/downloads/agent-2.0.0.msi",
            Checksum = "abc123",
            Size = 1024000,
            ReleaseNotes = "Nova versão com melhorias",
            IsRequired = false,
            IsCritical = false,
            MinimumVersion = new VersionInfo(1, 0, 0)
        };

        var jsonResponse = JsonSerializer.Serialize(updateInfo);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/updates/latest")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _updateService.CheckForUpdateAsync();

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be(updateInfo.Version);
        result.DownloadUrl.Should().Be(updateInfo.DownloadUrl);
        result.Checksum.Should().Be(updateInfo.Checksum);
        result.Size.Should().Be(updateInfo.Size);
        result.ReleaseNotes.Should().Be(updateInfo.ReleaseNotes);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenNoUpdateAvailable_ShouldReturnNull()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.NoContent);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/updates/latest")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _updateService.CheckForUpdateAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenApiError_ShouldThrowException()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/updates/latest")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act & Assert
        var act = async () => await _updateService.CheckForUpdateAsync();
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DownloadUpdateAsync_WithValidUpdateInfo_ShouldDownloadSuccessfully()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", // SHA256 of empty string
            Size = 0
        };

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("agent-2.0.0.msi")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _updateService.DownloadUpdateAsync(updateInfo);

        // Assert
        result.Should().NotBeNull();
        File.Exists(result).Should().BeTrue();
        
        // Cleanup
        if (File.Exists(result))
        {
            File.Delete(result);
        }
    }

    [Fact]
    public async Task DownloadUpdateAsync_WithInvalidChecksum_ShouldThrowException()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/agent-2.0.0.msi",
            Checksum = "invalid_checksum",
            Size = 0
        };

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("agent-2.0.0.msi")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act & Assert
        var act = async () => await _updateService.DownloadUpdateAsync(updateInfo);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*checksum*");
    }

    [Fact]
    public async Task ProcessUpdateAsync_WithValidUpdate_ShouldCompleteSuccessfully()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Size = 0,
            ReleaseNotes = "Test update"
        };

        // Setup HTTP response for download
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("agent-2.0.0.msi")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var progressReports = new List<UpdateStatus>();
        var progress = new Progress<UpdateStatus>(status => progressReports.Add(status));

        // Act
        var result = await _updateService.ProcessUpdateAsync(updateInfo, progress);

        // Assert
        result.Should().BeTrue();
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(s => s.State == UpdateState.Downloading);
        progressReports.Should().Contain(s => s.State == UpdateState.Downloaded);
        progressReports.Should().Contain(s => s.State == UpdateState.Installing);
        progressReports.Last().State.Should().Be(UpdateState.Completed);
    }

    [Fact]
    public async Task ProcessUpdateAsync_WithDownloadFailure_ShouldReturnFalse()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/agent-2.0.0.msi",
            Checksum = "invalid_checksum",
            Size = 0
        };

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("agent-2.0.0.msi")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var progressReports = new List<UpdateStatus>();
        var progress = new Progress<UpdateStatus>(status => progressReports.Add(status));

        // Act
        var result = await _updateService.ProcessUpdateAsync(updateInfo, progress);

        // Assert
        result.Should().BeFalse();
        progressReports.Should().NotBeEmpty();
        progressReports.Last().State.Should().Be(UpdateState.Failed);
    }

    [Fact]
    public async Task GetCurrentVersionAsync_ShouldReturnValidVersion()
    {
        // Act
        var version = await _updateService.GetCurrentVersionAsync();

        // Assert
        version.Should().NotBeNull();
        version.Major.Should().BeGreaterThanOrEqualTo(0);
        version.Minor.Should().BeGreaterThanOrEqualTo(0);
        version.Patch.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task IsUpdateAvailableAsync_WhenNewerVersionExists_ShouldReturnTrue()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(99, 0, 0), // Very high version number
            DownloadUrl = "https://api.example.com/downloads/agent-99.0.0.msi",
            Checksum = "abc123",
            Size = 1024000
        };

        var jsonResponse = JsonSerializer.Serialize(updateInfo);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/updates/latest")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _updateService.IsUpdateAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUpdateAvailableAsync_WhenNoNewerVersionExists_ShouldReturnFalse()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.NoContent);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/updates/latest")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _updateService.IsUpdateAvailableAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UpdateConfig_ShouldBeValidated()
    {
        // Arrange
        var config = _serviceProvider.GetRequiredService<UpdateConfig>();

        // Assert
        config.Should().NotBeNull();
        config.IsEnabled.Should().BeTrue();
        config.CheckIntervalMinutes.Should().BeGreaterThan(0);
        config.DownloadTimeout.Should().BeGreaterThan(TimeSpan.Zero);
        config.MaxRetries.Should().BeGreaterThan(0);
        config.DownloadPath.Should().NotBeNullOrEmpty();
        config.BackupPath.Should().NotBeNullOrEmpty();
        config.ApiBaseUrl.Should().NotBeNullOrEmpty();
        config.TrustedPublishers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateService_ShouldHandleMultipleSimultaneousRequests()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/agent-2.0.0.msi",
            Checksum = "abc123",
            Size = 1024000
        };

        var jsonResponse = JsonSerializer.Serialize(updateInfo);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/updates/latest")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var tasks = new List<Task<UpdateInfo?>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_updateService.CheckForUpdateAsync());
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(result => result.Should().NotBeNull());
        results.Should().AllSatisfy(result => result!.Version.Should().Be(updateInfo.Version));
    }

    [Fact]
    public async Task UpdateService_ShouldHandleNetworkTimeouts()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        // Act & Assert
        var act = async () => await _updateService.CheckForUpdateAsync();
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task UpdateService_ShouldHandleNetworkUnavailability()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unavailable"));

        // Act & Assert
        var act = async () => await _updateService.CheckForUpdateAsync();
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _mockHttpMessageHandler?.Dispose();
        
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}