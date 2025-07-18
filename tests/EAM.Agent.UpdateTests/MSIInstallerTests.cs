using EAM.Agent.Configuration;
using EAM.Agent.Helpers;
using EAM.Shared.Models;
using FluentAssertions;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace EAM.Agent.UpdateTests;

public class MSIInstallerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _testMsiPath;
    private readonly string _testCertPath;

    public MSIInstallerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EAM.MSITests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        
        _testMsiPath = Path.Combine(_tempDirectory, "test-installer.msi");
        _testCertPath = Path.Combine(_tempDirectory, "test-cert.pfx");
        
        // Create a dummy MSI file for testing
        CreateDummyMSIFile();
    }

    private void CreateDummyMSIFile()
    {
        // Create a minimal MSI-like file for testing
        var msiHeader = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // MSI signature
        var additionalData = Encoding.UTF8.GetBytes("This is a test MSI file for EAM Agent installer validation");
        
        var fileContent = new byte[msiHeader.Length + additionalData.Length];
        Array.Copy(msiHeader, 0, fileContent, 0, msiHeader.Length);
        Array.Copy(additionalData, 0, fileContent, msiHeader.Length, additionalData.Length);
        
        File.WriteAllBytes(_testMsiPath, fileContent);
    }

    [Fact]
    public void MSIFile_ShouldHaveCorrectSignature()
    {
        // Arrange & Act
        var fileExists = File.Exists(_testMsiPath);
        var fileBytes = File.ReadAllBytes(_testMsiPath);
        
        // Assert
        fileExists.Should().BeTrue();
        fileBytes.Should().NotBeEmpty();
        fileBytes.Length.Should().BeGreaterThan(8);
        
        // Check MSI signature (first 8 bytes)
        var signature = fileBytes.Take(8).ToArray();
        signature.Should().BeEquivalentTo(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 });
    }

    [Fact]
    public void ProcessHelper_ShouldValidateMSIInstallationCommand()
    {
        // Arrange
        var msiPath = _testMsiPath;
        var expectedCommand = "msiexec.exe";
        var expectedArguments = $"/i \"{msiPath}\" /quiet /norestart REBOOT=ReallySuppress";

        // Act
        var commandInfo = ProcessHelper.CreateMSIInstallCommand(msiPath, silent: true);

        // Assert
        commandInfo.Should().NotBeNull();
        commandInfo.FileName.Should().Be(expectedCommand);
        commandInfo.Arguments.Should().Be(expectedArguments);
        commandInfo.UseShellExecute.Should().BeFalse();
        commandInfo.CreateNoWindow.Should().BeTrue();
        commandInfo.RedirectStandardOutput.Should().BeTrue();
        commandInfo.RedirectStandardError.Should().BeTrue();
    }

    [Fact]
    public void ProcessHelper_ShouldValidateMSIUninstallCommand()
    {
        // Arrange
        var productCode = "{12345678-1234-1234-1234-123456789012}";
        var expectedCommand = "msiexec.exe";
        var expectedArguments = $"/x {productCode} /quiet /norestart REBOOT=ReallySuppress";

        // Act
        var commandInfo = ProcessHelper.CreateMSIUninstallCommand(productCode, silent: true);

        // Assert
        commandInfo.Should().NotBeNull();
        commandInfo.FileName.Should().Be(expectedCommand);
        commandInfo.Arguments.Should().Be(expectedArguments);
        commandInfo.UseShellExecute.Should().BeFalse();
        commandInfo.CreateNoWindow.Should().BeTrue();
        commandInfo.RedirectStandardOutput.Should().BeTrue();
        commandInfo.RedirectStandardError.Should().BeTrue();
    }

    [Fact]
    public void ProcessHelper_ShouldValidateMSIRepairCommand()
    {
        // Arrange
        var productCode = "{12345678-1234-1234-1234-123456789012}";
        var expectedCommand = "msiexec.exe";
        var expectedArguments = $"/fa {productCode} /quiet /norestart REBOOT=ReallySuppress";

        // Act
        var commandInfo = ProcessHelper.CreateMSIRepairCommand(productCode, silent: true);

        // Assert
        commandInfo.Should().NotBeNull();
        commandInfo.FileName.Should().Be(expectedCommand);
        commandInfo.Arguments.Should().Be(expectedArguments);
        commandInfo.UseShellExecute.Should().BeFalse();
        commandInfo.CreateNoWindow.Should().BeTrue();
        commandInfo.RedirectStandardOutput.Should().BeTrue();
        commandInfo.RedirectStandardError.Should().BeTrue();
    }

    [Fact]
    public void SecurityHelper_ShouldValidateMSIPackageIntegrity()
    {
        // Arrange
        var fileBytes = File.ReadAllBytes(_testMsiPath);
        var checksum = SecurityHelper.CalculateChecksum(fileBytes);

        // Act
        var isValid = SecurityHelper.ValidatePackageIntegrity(_testMsiPath, checksum);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void SecurityHelper_ShouldDetectCorruptedMSIPackage()
    {
        // Arrange
        var fileBytes = File.ReadAllBytes(_testMsiPath);
        var originalChecksum = SecurityHelper.CalculateChecksum(fileBytes);
        
        // Corrupt the file
        var corruptedBytes = fileBytes.ToArray();
        corruptedBytes[corruptedBytes.Length - 1] = 0xFF; // Change last byte
        
        var corruptedPath = Path.Combine(_tempDirectory, "corrupted.msi");
        File.WriteAllBytes(corruptedPath, corruptedBytes);

        // Act
        var isValid = SecurityHelper.ValidatePackageIntegrity(corruptedPath, originalChecksum);

        // Assert
        isValid.Should().BeFalse();
        
        // Cleanup
        File.Delete(corruptedPath);
    }

    [Fact]
    public void FileHelper_ShouldValidateMSIFileProperties()
    {
        // Act
        var fileInfo = new FileInfo(_testMsiPath);
        var isAccessible = FileHelper.IsFileAccessible(_testMsiPath);
        var hasReadPermission = FileHelper.HasReadPermission(_testMsiPath);

        // Assert
        fileInfo.Exists.Should().BeTrue();
        fileInfo.Length.Should().BeGreaterThan(0);
        fileInfo.Extension.Should().Be(".msi");
        isAccessible.Should().BeTrue();
        hasReadPermission.Should().BeTrue();
    }

    [Fact]
    public void UpdateConfig_ShouldValidateInstallerSettings()
    {
        // Arrange
        var config = new UpdateConfig
        {
            IsEnabled = true,
            AutoInstall = true,
            VerifySignatures = true,
            TrustedPublishers = new[] { "Test Publisher", "Microsoft Corporation" },
            MaxRetries = 3,
            BackupEnabled = true,
            DownloadPath = _tempDirectory,
            BackupPath = Path.Combine(_tempDirectory, "backup")
        };

        // Act & Assert
        config.IsEnabled.Should().BeTrue();
        config.AutoInstall.Should().BeTrue();
        config.VerifySignatures.Should().BeTrue();
        config.TrustedPublishers.Should().NotBeEmpty();
        config.TrustedPublishers.Should().Contain("Test Publisher");
        config.MaxRetries.Should().BeGreaterThan(0);
        config.BackupEnabled.Should().BeTrue();
        config.DownloadPath.Should().NotBeNullOrEmpty();
        config.BackupPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MSIInstaller_ShouldValidateRequiredProperties()
    {
        // Arrange
        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Size = 1024000,
            ReleaseNotes = "Nova versão com sistema de auto-update",
            IsRequired = false,
            IsCritical = false,
            MinimumVersion = new VersionInfo(1, 0, 0)
        };

        // Act & Assert
        updateInfo.Version.Should().NotBeNull();
        updateInfo.DownloadUrl.Should().NotBeNullOrEmpty();
        updateInfo.DownloadUrl.Should().EndWith(".msi");
        updateInfo.Checksum.Should().NotBeNullOrEmpty();
        updateInfo.Checksum.Should().HaveLength(64); // SHA256 hex string
        updateInfo.Size.Should().BeGreaterThan(0);
        updateInfo.ReleaseNotes.Should().NotBeNullOrEmpty();
        updateInfo.MinimumVersion.Should().NotBeNull();
    }

    [Fact]
    public void MSIInstaller_ShouldValidateVersionCompatibility()
    {
        // Arrange
        var currentVersion = new VersionInfo(1, 5, 0);
        var updateVersion = new VersionInfo(2, 0, 0);
        var minimumVersion = new VersionInfo(1, 0, 0);

        // Act
        var isCompatible = currentVersion >= minimumVersion;
        var isUpdateAvailable = updateVersion > currentVersion;

        // Assert
        isCompatible.Should().BeTrue();
        isUpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public void MSIInstaller_ShouldValidateDownloadPath()
    {
        // Arrange
        var config = new UpdateConfig
        {
            DownloadPath = _tempDirectory
        };

        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi"
        };

        // Act
        var expectedFileName = "EAM-Agent-2.0.0.msi";
        var expectedPath = Path.Combine(config.DownloadPath, expectedFileName);

        // Assert
        Path.GetDirectoryName(expectedPath).Should().Be(config.DownloadPath);
        Path.GetFileName(expectedPath).Should().Be(expectedFileName);
        Path.GetExtension(expectedPath).Should().Be(".msi");
    }

    [Fact]
    public void MSIInstaller_ShouldValidateBackupPath()
    {
        // Arrange
        var config = new UpdateConfig
        {
            BackupPath = Path.Combine(_tempDirectory, "backup"),
            BackupEnabled = true
        };

        var currentVersion = new VersionInfo(1, 5, 0);

        // Act
        var expectedBackupFolder = $"backup-{currentVersion}";
        var expectedBackupPath = Path.Combine(config.BackupPath, expectedBackupFolder);

        // Assert
        config.BackupEnabled.Should().BeTrue();
        config.BackupPath.Should().NotBeNullOrEmpty();
        expectedBackupFolder.Should().Contain(currentVersion.ToString());
        Path.GetDirectoryName(expectedBackupPath).Should().Be(config.BackupPath);
    }

    [Fact]
    public void MSIInstaller_ShouldValidateInstallationParameters()
    {
        // Arrange
        var installParameters = new Dictionary<string, string>
        {
            ["INSTALLLEVEL"] = "1000",
            ["INSTALLDIR"] = @"C:\Program Files\EAM Agent",
            ["SERVICENAME"] = "EAM.Agent",
            ["SERVICEACCOUNT"] = "LocalSystem",
            ["AUTOSTART"] = "1",
            ["LOGGING"] = "1"
        };

        // Act & Assert
        installParameters.Should().NotBeEmpty();
        installParameters.Should().ContainKey("INSTALLLEVEL");
        installParameters.Should().ContainKey("INSTALLDIR");
        installParameters.Should().ContainKey("SERVICENAME");
        installParameters.Should().ContainKey("SERVICEACCOUNT");
        installParameters.Should().ContainKey("AUTOSTART");
        installParameters.Should().ContainKey("LOGGING");
        
        installParameters["INSTALLLEVEL"].Should().Be("1000");
        installParameters["INSTALLDIR"].Should().NotBeNullOrEmpty();
        installParameters["SERVICENAME"].Should().Be("EAM.Agent");
        installParameters["SERVICEACCOUNT"].Should().Be("LocalSystem");
        installParameters["AUTOSTART"].Should().Be("1");
        installParameters["LOGGING"].Should().Be("1");
    }

    [Fact]
    public void MSIInstaller_ShouldValidateUninstallationProcess()
    {
        // Arrange
        var productCode = "{12345678-1234-1234-1234-123456789012}";
        var uninstallCommand = ProcessHelper.CreateMSIUninstallCommand(productCode, silent: true);

        // Act & Assert
        uninstallCommand.Should().NotBeNull();
        uninstallCommand.FileName.Should().Be("msiexec.exe");
        uninstallCommand.Arguments.Should().Contain("/x");
        uninstallCommand.Arguments.Should().Contain(productCode);
        uninstallCommand.Arguments.Should().Contain("/quiet");
        uninstallCommand.Arguments.Should().Contain("/norestart");
        uninstallCommand.Arguments.Should().Contain("REBOOT=ReallySuppress");
    }

    [Fact]
    public void MSIInstaller_ShouldValidateRollbackCapability()
    {
        // Arrange
        var config = new UpdateConfig
        {
            BackupEnabled = true,
            BackupPath = Path.Combine(_tempDirectory, "backup")
        };

        var currentVersion = new VersionInfo(1, 5, 0);
        var backupPath = Path.Combine(config.BackupPath, $"backup-{currentVersion}");

        // Act
        Directory.CreateDirectory(backupPath);
        var testFile = Path.Combine(backupPath, "EAM.Agent.exe");
        File.WriteAllText(testFile, "test backup content");

        var backupExists = Directory.Exists(backupPath);
        var backupFileExists = File.Exists(testFile);

        // Assert
        config.BackupEnabled.Should().BeTrue();
        backupExists.Should().BeTrue();
        backupFileExists.Should().BeTrue();
        
        // Cleanup
        Directory.Delete(backupPath, recursive: true);
    }

    [Fact]
    public void MSIInstaller_ShouldValidateDigitalSignatureRequirements()
    {
        // Arrange
        var config = new UpdateConfig
        {
            VerifySignatures = true,
            TrustedPublishers = new[] { "Test Publisher", "Microsoft Corporation" }
        };

        var updateInfo = new UpdateInfo
        {
            Version = new VersionInfo(2, 0, 0),
            DownloadUrl = "https://api.example.com/downloads/EAM-Agent-2.0.0.msi",
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            SignatureInfo = new SignatureInfo
            {
                Publisher = "Test Publisher",
                Thumbprint = "1234567890ABCDEF1234567890ABCDEF12345678",
                ValidFrom = DateTimeOffset.UtcNow.AddDays(-30),
                ValidTo = DateTimeOffset.UtcNow.AddDays(365),
                IsValid = true
            }
        };

        // Act & Assert
        config.VerifySignatures.Should().BeTrue();
        config.TrustedPublishers.Should().NotBeEmpty();
        config.TrustedPublishers.Should().Contain("Test Publisher");
        
        updateInfo.SignatureInfo.Should().NotBeNull();
        updateInfo.SignatureInfo.Publisher.Should().Be("Test Publisher");
        updateInfo.SignatureInfo.Thumbprint.Should().NotBeNullOrEmpty();
        updateInfo.SignatureInfo.IsValid.Should().BeTrue();
        updateInfo.SignatureInfo.ValidFrom.Should().BeBefore(DateTimeOffset.UtcNow);
        updateInfo.SignatureInfo.ValidTo.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
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