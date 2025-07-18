using EAM.Agent.Services;
using EAM.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EAM.Agent.UpdateTests;

public class VersionManagerTests
{
    private readonly Mock<ILogger<VersionManager>> _loggerMock;
    private readonly VersionManager _versionManager;

    public VersionManagerTests()
    {
        _loggerMock = new Mock<ILogger<VersionManager>>();
        _versionManager = new VersionManager(_loggerMock.Object);
    }

    [Fact]
    public void GetCurrentVersion_ShouldReturnValidVersion()
    {
        // Act
        var version = _versionManager.GetCurrentVersion();

        // Assert
        version.Should().NotBeNull();
        version.Major.Should().BeGreaterThan(0);
        version.Minor.Should().BeGreaterOrEqualTo(0);
        version.Patch.Should().BeGreaterOrEqualTo(0);
    }

    [Theory]
    [InlineData("5.0.0", "5.0.1", true)]
    [InlineData("5.0.0", "5.1.0", true)]
    [InlineData("5.0.0", "6.0.0", true)]
    [InlineData("5.0.1", "5.0.0", false)]
    [InlineData("5.1.0", "5.0.0", false)]
    [InlineData("6.0.0", "5.0.0", false)]
    [InlineData("5.0.0", "5.0.0", false)]
    public void IsUpdateAvailable_ShouldReturnCorrectResult(string current, string available, bool expected)
    {
        // Arrange
        var currentVersion = VersionInfo.Parse(current);
        var availableVersion = VersionInfo.Parse(available);

        // Act
        var result = _versionManager.IsUpdateAvailable(currentVersion, availableVersion);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("5.0.0", "5.0.1", "4.9.0", false)]
    [InlineData("4.8.0", "5.0.0", "4.9.0", true)]
    [InlineData("4.9.0", "5.0.0", "4.9.0", false)]
    [InlineData("4.8.5", "5.0.0", "4.9.0", true)]
    public void IsUpdateRequired_ShouldReturnCorrectResult(string current, string available, string minimum, bool expected)
    {
        // Arrange
        var currentVersion = VersionInfo.Parse(current);
        var availableVersion = VersionInfo.Parse(available);
        var minimumVersion = VersionInfo.Parse(minimum);

        // Act
        var result = _versionManager.IsUpdateRequired(currentVersion, availableVersion, minimumVersion);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("5.0.0", "5.0.1", null, UpdatePriority.Low)]
    [InlineData("5.0.0", "5.1.0", null, UpdatePriority.Medium)]
    [InlineData("5.0.0", "6.0.0", null, UpdatePriority.High)]
    [InlineData("4.8.0", "5.0.0", "4.9.0", UpdatePriority.Critical)]
    [InlineData("5.0.0", "5.0.0", null, UpdatePriority.None)]
    public void GetUpdatePriority_ShouldReturnCorrectPriority(string current, string available, string? minimum, UpdatePriority expected)
    {
        // Arrange
        var currentVersion = VersionInfo.Parse(current);
        var availableVersion = VersionInfo.Parse(available);
        var minimumVersion = minimum != null ? VersionInfo.Parse(minimum) : null;

        // Act
        var result = _versionManager.GetUpdatePriority(currentVersion, availableVersion, minimumVersion);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("5.0.0", "5.0.1-beta", false, false)]
    [InlineData("5.0.0", "5.0.1-beta", true, true)]
    [InlineData("5.0.0", "5.0.1", false, true)]
    [InlineData("5.0.0", "5.0.1", true, true)]
    public void IsUpdateAvailable_WithPreRelease_ShouldReturnCorrectResult(string current, string available, bool includePreRelease, bool expected)
    {
        // Arrange
        var currentVersion = VersionInfo.Parse(current);
        var availableVersion = VersionInfo.Parse(available);

        // Act
        var result = _versionManager.IsUpdateAvailable(currentVersion, availableVersion, includePreRelease);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetVersionString_ShouldReturnValidString()
    {
        // Act
        var versionString = _versionManager.GetVersionString();

        // Assert
        versionString.Should().NotBeNullOrEmpty();
        versionString.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    [Fact]
    public void GetVersionMetadata_ShouldReturnValidMetadata()
    {
        // Act
        var metadata = _versionManager.GetVersionMetadata();

        // Assert
        metadata.Should().NotBeNull();
        metadata.Should().ContainKey("version");
        metadata.Should().ContainKey("major");
        metadata.Should().ContainKey("minor");
        metadata.Should().ContainKey("patch");
        metadata["version"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InvalidateCache_ShouldForceVersionRedetection()
    {
        // Arrange
        var version1 = _versionManager.GetCurrentVersion();

        // Act
        _versionManager.InvalidateCache();
        var version2 = _versionManager.GetCurrentVersion();

        // Assert
        version1.Should().NotBeNull();
        version2.Should().NotBeNull();
        // Both versions should be equal since we're testing the same assembly
        version1.ToString().Should().Be(version2.ToString());
    }
}