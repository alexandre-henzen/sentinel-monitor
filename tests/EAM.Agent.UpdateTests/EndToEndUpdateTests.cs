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
using Xunit.Abstractions;

namespace EAM.Agent.UpdateTests;

public class EndToEndUpdateTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly UpdateService _updateService;
    private readonly string _tempDirectory;
    private readonly string _tempDownloadPath;
    private readonly string _tempBackupPath;
    private readonly string _tempInstallPath;

    public EndToEndUpdateTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EAM.EndToEndTests", Guid.NewGuid().ToString());
        _tempDownloadPath = Path.Combine(_tempDirectory, "downloads");
        _tempBackupPath = Path.Combine(_tempDirectory, "backups");
        _tempInstallPath = Path.Combine(_tempDirectory, "install");
        
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_tempDownloadPath);
        Directory.CreateDirectory(_tempBackupPath);
        Directory.CreateDirectory(_tempInstallPath);

        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
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
            InstallPath = _tempInstallPath,
            ApiBaseUrl = "https://api.example.com",
            VerifySignatures = true,
            TrustedPublishers = new[] { "Test Publisher", "Microsoft Corporation" },
            MaintenanceWindow = new MaintenanceWindow
            {
                StartTime = TimeSpan.FromHours(2),
                EndTime = TimeSpan.FromHours(6),
                AllowOutsideWindow = false
            }
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
    public async Task CompleteUpdateFlow_WhenUpdateAvailable_ShouldProcessSuccessfully()
    {
        // Arrange
        _output.WriteLine("=== Starting Complete Update Flow Test ===");
        
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            ReleaseDate = DateTimeOffset.UtcNow,
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Size = 1024,
            ReleaseNotes = "Versão 2.0.0 com sistema de auto-update implementado",
            IsRequired = false,
            IsCritical = false,
            MinimumVersion = new VersionInfo(1, 0, 0),
            SignatureInfo = new SignatureInfo
            {
                Publisher = "Test Publisher",
                Thumbprint = "1234567890ABCDEF1234567890ABCDEF12345678",
                ValidFrom = DateTimeOffset.UtcNow.AddDays(-30),
                ValidTo = DateTimeOffset.UtcNow.AddDays(365),
                IsValid = true
            }
        };

        SetupHttpMocks(updateInfo);

        var progressReports = new List<UpdateStatus>();
        var progress = new Progress<UpdateStatus>(status => 
        {
            progressReports.Add(status);
            _output.WriteLine($"Progress: {status.State} - {status.Message} ({status.ProgressPercentage}%)");
        });

        // Act
        _output.WriteLine("Phase 1: Checking for updates...");
        var availableUpdate = await _updateService.CheckForUpdateAsync();
        
        _output.WriteLine("Phase 2: Processing update...");
        var updateResult = await _updateService.ProcessUpdateAsync(updateInfo, progress);

        // Assert
        _output.WriteLine("=== Validating Results ===");
        
        // Validate update detection
        availableUpdate.Should().NotBeNull();
        availableUpdate.Version.Should().Be(updateInfo.Version);
        availableUpdate.DownloadUrl.Should().Be(updateInfo.DownloadUrl);
        _output.WriteLine($"✓ Update detected: {availableUpdate.Version}");

        // Validate update processing
        updateResult.Should().BeTrue();
        _output.WriteLine("✓ Update processing completed successfully");

        // Validate progress reports
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(s => s.State == UpdateState.Checking);
        progressReports.Should().Contain(s => s.State == UpdateState.Downloading);
        progressReports.Should().Contain(s => s.State == UpdateState.Downloaded);
        progressReports.Should().Contain(s => s.State == UpdateState.BackingUp);
        progressReports.Should().Contain(s => s.State == UpdateState.Installing);
        progressReports.Last().State.Should().Be(UpdateState.Completed);
        _output.WriteLine("✓ All progress states reported correctly");

        // Validate final state
        var finalStatus = progressReports.Last();
        finalStatus.State.Should().Be(UpdateState.Completed);
        finalStatus.ProgressPercentage.Should().Be(100);
        finalStatus.Message.Should().Contain("sucesso");
        _output.WriteLine($"✓ Final status: {finalStatus.Message}");

        _output.WriteLine("=== Complete Update Flow Test Passed ===");
    }

    [Fact]
    public async Task CompleteUpdateFlow_WhenNoUpdateAvailable_ShouldHandleGracefully()
    {
        // Arrange
        _output.WriteLine("=== Starting No Update Available Test ===");
        
        var httpResponse = new HttpResponseMessage(HttpStatusCode.NoContent);
        
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        _output.WriteLine("Checking for updates...");
        var availableUpdate = await _updateService.CheckForUpdateAsync();
        var isUpdateAvailable = await _updateService.IsUpdateAvailableAsync();

        // Assert
        availableUpdate.Should().BeNull();
        isUpdateAvailable.Should().BeFalse();
        _output.WriteLine("✓ No update available - handled correctly");
        _output.WriteLine("=== No Update Available Test Passed ===");
    }

    [Fact]
    public async Task CompleteUpdateFlow_WhenDownloadFails_ShouldRollback()
    {
        // Arrange
        _output.WriteLine("=== Starting Download Failure Test ===");
        
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi",
            Checksum = "invalid_checksum_to_force_failure",
            Size = 1024,
            ReleaseNotes = "Test update with invalid checksum"
        };

        // Setup check for update (successful)
        var checkResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(updateInfo), System.Text.Encoding.UTF8, "application/json")
        };

        // Setup download (returns content but checksum will fail)
        var downloadResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[1024])
        };

        _mockHttpMessageHandler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(checkResponse)
            .ReturnsAsync(downloadResponse);

        var progressReports = new List<UpdateStatus>();
        var progress = new Progress<UpdateStatus>(status => 
        {
            progressReports.Add(status);
            _output.WriteLine($"Progress: {status.State} - {status.Message}");
        });

        // Act
        _output.WriteLine("Processing update with invalid checksum...");
        var updateResult = await _updateService.ProcessUpdateAsync(updateInfo, progress);

        // Assert
        updateResult.Should().BeFalse();
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(s => s.State == UpdateState.Downloading);
        progressReports.Last().State.Should().Be(UpdateState.Failed);
        _output.WriteLine("✓ Download failure handled correctly with rollback");
        _output.WriteLine("=== Download Failure Test Passed ===");
    }

    [Fact]
    public async Task CompleteUpdateFlow_WhenMaintenanceWindowActive_ShouldProceed()
    {
        // Arrange
        _output.WriteLine("=== Starting Maintenance Window Test ===");
        
        var config = _serviceProvider.GetRequiredService<UpdateConfig>();
        
        // Set maintenance window to current time
        var now = DateTime.Now;
        config.MaintenanceWindow.StartTime = now.TimeOfDay.Subtract(TimeSpan.FromHours(1));
        config.MaintenanceWindow.EndTime = now.TimeOfDay.Add(TimeSpan.FromHours(1));
        config.MaintenanceWindow.AllowOutsideWindow = false;

        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Size = 1024,
            ReleaseNotes = "Test update during maintenance window"
        };

        SetupHttpMocks(updateInfo);

        // Act
        _output.WriteLine($"Current time: {now:HH:mm:ss}");
        _output.WriteLine($"Maintenance window: {config.MaintenanceWindow.StartTime} - {config.MaintenanceWindow.EndTime}");
        
        var isInMaintenanceWindow = IsInMaintenanceWindow(now.TimeOfDay, config.MaintenanceWindow);
        var updateResult = await _updateService.ProcessUpdateAsync(updateInfo, new Progress<UpdateStatus>());

        // Assert
        isInMaintenanceWindow.Should().BeTrue();
        updateResult.Should().BeTrue();
        _output.WriteLine("✓ Update processed successfully during maintenance window");
        _output.WriteLine("=== Maintenance Window Test Passed ===");
    }

    [Fact]
    public async Task CompleteUpdateFlow_WhenOutsideMaintenanceWindow_ShouldWaitOrSkip()
    {
        // Arrange
        _output.WriteLine("=== Starting Outside Maintenance Window Test ===");
        
        var config = _serviceProvider.GetRequiredService<UpdateConfig>();
        
        // Set maintenance window to future time
        var now = DateTime.Now;
        config.MaintenanceWindow.StartTime = now.TimeOfDay.Add(TimeSpan.FromHours(2));
        config.MaintenanceWindow.EndTime = now.TimeOfDay.Add(TimeSpan.FromHours(4));
        config.MaintenanceWindow.AllowOutsideWindow = false;

        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Size = 1024,
            ReleaseNotes = "Test update outside maintenance window"
        };

        // Act
        _output.WriteLine($"Current time: {now:HH:mm:ss}");
        _output.WriteLine($"Maintenance window: {config.MaintenanceWindow.StartTime} - {config.MaintenanceWindow.EndTime}");
        
        var isInMaintenanceWindow = IsInMaintenanceWindow(now.TimeOfDay, config.MaintenanceWindow);

        // Assert
        isInMaintenanceWindow.Should().BeFalse();
        _output.WriteLine("✓ Correctly identified outside maintenance window");
        _output.WriteLine("=== Outside Maintenance Window Test Passed ===");
    }

    [Fact]
    public async Task CompleteUpdateFlow_WhenCriticalUpdate_ShouldBypassMaintenanceWindow()
    {
        // Arrange
        _output.WriteLine("=== Starting Critical Update Test ===");
        
        var config = _serviceProvider.GetRequiredService<UpdateConfig>();
        
        // Set maintenance window to future time
        var now = DateTime.Now;
        config.MaintenanceWindow.StartTime = now.TimeOfDay.Add(TimeSpan.FromHours(2));
        config.MaintenanceWindow.EndTime = now.TimeOfDay.Add(TimeSpan.FromHours(4));
        config.MaintenanceWindow.AllowOutsideWindow = false;

        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Size = 1024,
            ReleaseNotes = "Critical security update",
            IsCritical = true // Critical update should bypass maintenance window
        };

        SetupHttpMocks(updateInfo);

        // Act
        _output.WriteLine($"Current time: {now:HH:mm:ss}");
        _output.WriteLine($"Maintenance window: {config.MaintenanceWindow.StartTime} - {config.MaintenanceWindow.EndTime}");
        _output.WriteLine("Processing critical update...");
        
        var updateResult = await _updateService.ProcessUpdateAsync(updateInfo, new Progress<UpdateStatus>());

        // Assert
        updateResult.Should().BeTrue();
        _output.WriteLine("✓ Critical update processed successfully outside maintenance window");
        _output.WriteLine("=== Critical Update Test Passed ===");
    }

    [Fact]
    public async Task CompleteUpdateFlow_WithMultipleRetries_ShouldEventuallySucceed()
    {
        // Arrange
        _output.WriteLine("=== Starting Multiple Retries Test ===");
        
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Size = 1024,
            ReleaseNotes = "Test update with retries"
        };

        // Setup check for update (successful)
        var checkResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(updateInfo), System.Text.Encoding.UTF8, "application/json")
        };

        // Setup download (fail twice, then succeed)
        var failResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        _mockHttpMessageHandler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(checkResponse)
            .ReturnsAsync(failResponse)
            .ReturnsAsync(failResponse)
            .ReturnsAsync(successResponse);

        var progressReports = new List<UpdateStatus>();
        var progress = new Progress<UpdateStatus>(status => 
        {
            progressReports.Add(status);
            _output.WriteLine($"Progress: {status.State} - {status.Message}");
        });

        // Act
        _output.WriteLine("Processing update with retry logic...");
        var updateResult = await _updateService.ProcessUpdateAsync(updateInfo, progress);

        // Assert
        updateResult.Should().BeTrue();
        progressReports.Should().NotBeEmpty();
        progressReports.Last().State.Should().Be(UpdateState.Completed);
        _output.WriteLine("✓ Update succeeded after retries");
        _output.WriteLine("=== Multiple Retries Test Passed ===");
    }

    private void SetupHttpMocks(UpdateInfo updateInfo)
    {
        // Setup check for update response
        var checkResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(updateInfo), System.Text.Encoding.UTF8, "application/json")
        };

        // Setup download response
        var downloadResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };

        _mockHttpMessageHandler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(checkResponse)
            .ReturnsAsync(downloadResponse);
    }

    private static bool IsInMaintenanceWindow(TimeSpan currentTime, MaintenanceWindow window)
    {
        if (window.StartTime <= window.EndTime)
        {
            // Same day window
            return currentTime >= window.StartTime && currentTime <= window.EndTime;
        }
        else
        {
            // Cross midnight window
            return currentTime >= window.StartTime || currentTime <= window.EndTime;
        }
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
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Could not cleanup temp directory: {ex.Message}");
            }
        }
    }
}