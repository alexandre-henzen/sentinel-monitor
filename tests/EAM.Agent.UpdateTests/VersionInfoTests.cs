using EAM.Shared.Models;
using FluentAssertions;
using Xunit;

namespace EAM.Agent.UpdateTests;

public class VersionInfoTests
{
    [Theory]
    [InlineData("1.0.0", 1, 0, 0, "", "")]
    [InlineData("2.5.10", 2, 5, 10, "", "")]
    [InlineData("1.0.0-alpha", 1, 0, 0, "alpha", "")]
    [InlineData("1.0.0-beta.1", 1, 0, 0, "beta.1", "")]
    [InlineData("1.0.0+build.1", 1, 0, 0, "", "build.1")]
    [InlineData("1.0.0-alpha+build.1", 1, 0, 0, "alpha", "build.1")]
    [InlineData("10.20.30-RC.1+build.123", 10, 20, 30, "RC.1", "build.123")]
    public void Parse_WithValidVersionString_ShouldReturnCorrectVersionInfo(
        string versionString, 
        int expectedMajor, 
        int expectedMinor, 
        int expectedPatch, 
        string expectedPreRelease, 
        string expectedBuild)
    {
        // Act
        var version = VersionInfo.Parse(versionString);

        // Assert
        version.Should().NotBeNull();
        version.Major.Should().Be(expectedMajor);
        version.Minor.Should().Be(expectedMinor);
        version.Patch.Should().Be(expectedPatch);
        version.PreRelease.Should().Be(expectedPreRelease);
        version.Build.Should().Be(expectedBuild);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("a.b.c")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0-alpha-")]
    [InlineData("1.0.0+build+")]
    public void Parse_WithInvalidVersionString_ShouldThrowException(string versionString)
    {
        // Act & Assert
        var action = () => VersionInfo.Parse(versionString);
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("2.5.10", true)]
    [InlineData("1.0.0-alpha", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    [InlineData("1.0", false)]
    public void TryParse_WithVariousInputs_ShouldReturnExpectedResult(string versionString, bool expectedSuccess)
    {
        // Act
        var success = VersionInfo.TryParse(versionString, out var version);

        // Assert
        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            version.Should().NotBeNull();
        }
        else
        {
            version.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0-alpha", "1.0.0-alpha")]
    [InlineData("1.0.0+build", "1.0.0+build")]
    [InlineData("1.0.0-alpha+build", "1.0.0-alpha+build")]
    public void ToString_ShouldReturnOriginalString(string versionString, string expected)
    {
        // Arrange
        var version = VersionInfo.Parse(versionString);

        // Act
        var result = version.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0", "1.1.0", -1)]
    [InlineData("1.1.0", "1.0.0", 1)]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("2.0.0", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.0-alpha", 1)]
    [InlineData("1.0.0-alpha", "1.0.0", -1)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", -1)]
    [InlineData("1.0.0-beta", "1.0.0-alpha", 1)]
    public void CompareTo_ShouldReturnCorrectComparison(string version1, string version2, int expected)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1.CompareTo(v2);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.1", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.1.0", "1.0.0", false)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("2.0.0", "1.0.0", false)]
    [InlineData("1.0.0-alpha", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0-alpha", false)]
    public void LessThanOperator_ShouldReturnCorrectResult(string version1, string version2, bool expected)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1 < v2;

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.1.0", false)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.0", "2.0.0", false)]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.0.0-alpha", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0-alpha", true)]
    public void GreaterThanOperator_ShouldReturnCorrectResult(string version1, string version2, bool expected)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1 > v2;

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.1", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.1.0", "1.0.0", false)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("2.0.0", "1.0.0", false)]
    [InlineData("1.0.0-alpha", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0-alpha", false)]
    public void LessThanOrEqualOperator_ShouldReturnCorrectResult(string version1, string version2, bool expected)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1 <= v2;

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.1.0", false)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.0", "2.0.0", false)]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.0.0-alpha", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0-alpha", true)]
    public void GreaterThanOrEqualOperator_ShouldReturnCorrectResult(string version1, string version2, bool expected)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1 >= v2;

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.0.0-alpha", "1.0.0-alpha", true)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", false)]
    [InlineData("1.0.0+build1", "1.0.0+build2", true)] // Build metadata should be ignored in equality
    public void EqualityOperator_ShouldReturnCorrectResult(string version1, string version2, bool expected)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1 == v2;

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0-alpha", "1.0.0-alpha", false)]
    [InlineData("1.0.0-alpha", "1.0.0-beta", true)]
    [InlineData("1.0.0+build1", "1.0.0+build2", false)] // Build metadata should be ignored in inequality
    public void InequalityOperator_ShouldReturnCorrectResult(string version1, string version2, bool expected)
    {
        // Arrange
        var v1 = VersionInfo.Parse(version1);
        var v2 = VersionInfo.Parse(version2);

        // Act
        var result = v1 != v2;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Constructor_WithParameters_ShouldCreateCorrectVersionInfo()
    {
        // Act
        var version = new VersionInfo(1, 2, 3, "alpha", "build");

        // Assert
        version.Major.Should().Be(1);
        version.Minor.Should().Be(2);
        version.Patch.Should().Be(3);
        version.PreRelease.Should().Be("alpha");
        version.Build.Should().Be("build");
    }

    [Fact]
    public void Constructor_WithDefaultParameters_ShouldCreateCorrectVersionInfo()
    {
        // Act
        var version = new VersionInfo(1, 2, 3);

        // Assert
        version.Major.Should().Be(1);
        version.Minor.Should().Be(2);
        version.Patch.Should().Be(3);
        version.PreRelease.Should().Be("");
        version.Build.Should().Be("");
    }

    [Fact]
    public void DefaultConstructor_ShouldCreateZeroVersion()
    {
        // Act
        var version = new VersionInfo();

        // Assert
        version.Major.Should().Be(0);
        version.Minor.Should().Be(0);
        version.Patch.Should().Be(0);
        version.PreRelease.Should().Be("");
        version.Build.Should().Be("");
    }

    [Fact]
    public void Equals_WithSameVersion_ShouldReturnTrue()
    {
        // Arrange
        var v1 = new VersionInfo(1, 0, 0);
        var v2 = new VersionInfo(1, 0, 0);

        // Act
        var result = v1.Equals(v2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentVersion_ShouldReturnFalse()
    {
        // Arrange
        var v1 = new VersionInfo(1, 0, 0);
        var v2 = new VersionInfo(1, 0, 1);

        // Act
        var result = v1.Equals(v2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        // Arrange
        var v1 = new VersionInfo(1, 0, 0);

        // Act
        var result = v1.Equals(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var v1 = new VersionInfo(1, 0, 0);
        var other = "1.0.0";

        // Act
        var result = v1.Equals(other);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_WithSameVersion_ShouldReturnSameHashCode()
    {
        // Arrange
        var v1 = new VersionInfo(1, 0, 0, "alpha", "build");
        var v2 = new VersionInfo(1, 0, 0, "alpha", "build");

        // Act
        var hash1 = v1.GetHashCode();
        var hash2 = v2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_WithDifferentVersion_ShouldReturnDifferentHashCode()
    {
        // Arrange
        var v1 = new VersionInfo(1, 0, 0);
        var v2 = new VersionInfo(1, 0, 1);

        // Act
        var hash1 = v1.GetHashCode();
        var hash2 = v2.GetHashCode();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void CompareTo_WithNull_ShouldReturnPositive()
    {
        // Arrange
        var v1 = new VersionInfo(1, 0, 0);

        // Act
        var result = v1.CompareTo(null);

        // Assert
        result.Should().BePositive();
    }
}