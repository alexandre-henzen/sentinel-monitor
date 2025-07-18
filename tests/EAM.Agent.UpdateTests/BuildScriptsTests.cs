using FluentAssertions;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace EAM.Agent.UpdateTests;

public class BuildScriptsTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _projectRoot;
    private readonly string _scriptsPath;

    public BuildScriptsTests(ITestOutputHelper output)
    {
        _output = output;
        _projectRoot = GetProjectRoot();
        _scriptsPath = Path.Combine(_projectRoot, "tools", "installer", "Scripts");
    }

    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "EAM.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find project root");
    }

    [Fact]
    public void BuildScripts_ShouldExistInCorrectLocation()
    {
        // Arrange & Act
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var masterScriptPath = Path.Combine(_scriptsPath, "build-and-sign.ps1");

        // Assert
        Directory.Exists(_scriptsPath).Should().BeTrue($"Scripts directory should exist at {_scriptsPath}");
        File.Exists(buildScriptPath).Should().BeTrue($"Build script should exist at {buildScriptPath}");
        File.Exists(signScriptPath).Should().BeTrue($"Sign script should exist at {signScriptPath}");
        File.Exists(masterScriptPath).Should().BeTrue($"Master script should exist at {masterScriptPath}");
        
        _output.WriteLine($"✓ All build scripts found at {_scriptsPath}");
    }

    [Fact]
    public void BuildScript_ShouldHaveCorrectStructure()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var scriptContent = File.ReadAllText(buildScriptPath);

        // Assert
        scriptContent.Should().NotBeNullOrEmpty();
        scriptContent.Should().Contain("param(");
        scriptContent.Should().Contain("Configuration");
        scriptContent.Should().Contain("OutputPath");
        scriptContent.Should().Contain("dotnet");
        scriptContent.Should().Contain("msbuild");
        scriptContent.Should().Contain("wix");
        scriptContent.Should().Contain("Write-Host");
        scriptContent.Should().Contain("try");
        scriptContent.Should().Contain("catch");
        
        _output.WriteLine("✓ Build script has correct structure with parameters, logging, and error handling");
    }

    [Fact]
    public void SignScript_ShouldHaveCorrectStructure()
    {
        // Arrange
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var scriptContent = File.ReadAllText(signScriptPath);

        // Assert
        scriptContent.Should().NotBeNullOrEmpty();
        scriptContent.Should().Contain("param(");
        scriptContent.Should().Contain("CertificatePath");
        scriptContent.Should().Contain("CertificatePassword");
        scriptContent.Should().Contain("MSIPath");
        scriptContent.Should().Contain("signtool");
        scriptContent.Should().Contain("Write-Host");
        scriptContent.Should().Contain("try");
        scriptContent.Should().Contain("catch");
        scriptContent.Should().Contain("Test-Path");
        
        _output.WriteLine("✓ Sign script has correct structure with certificate handling and validation");
    }

    [Fact]
    public void MasterScript_ShouldHaveCorrectStructure()
    {
        // Arrange
        var masterScriptPath = Path.Combine(_scriptsPath, "build-and-sign.ps1");
        var scriptContent = File.ReadAllText(masterScriptPath);

        // Assert
        scriptContent.Should().NotBeNullOrEmpty();
        scriptContent.Should().Contain("param(");
        scriptContent.Should().Contain("Configuration");
        scriptContent.Should().Contain("CertificatePath");
        scriptContent.Should().Contain("build-installer.ps1");
        scriptContent.Should().Contain("sign-installer.ps1");
        scriptContent.Should().Contain("Write-Host");
        scriptContent.Should().Contain("try");
        scriptContent.Should().Contain("catch");
        
        _output.WriteLine("✓ Master script has correct structure and calls other scripts");
    }

    [Fact]
    public void BuildScript_ShouldHaveValidParameters()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var scriptContent = File.ReadAllText(buildScriptPath);

        // Act
        var paramMatches = Regex.Matches(scriptContent, @"\$(\w+)", RegexOptions.IgnoreCase);
        var parameterNames = paramMatches.Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

        // Assert
        parameterNames.Should().Contain("Configuration");
        parameterNames.Should().Contain("OutputPath");
        parameterNames.Should().Contain("ProjectPath");
        
        _output.WriteLine($"✓ Build script parameters found: {string.Join(", ", parameterNames)}");
    }

    [Fact]
    public void SignScript_ShouldHaveValidParameters()
    {
        // Arrange
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var scriptContent = File.ReadAllText(signScriptPath);

        // Act
        var paramMatches = Regex.Matches(scriptContent, @"\$(\w+)", RegexOptions.IgnoreCase);
        var parameterNames = paramMatches.Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

        // Assert
        parameterNames.Should().Contain("CertificatePath");
        parameterNames.Should().Contain("CertificatePassword");
        parameterNames.Should().Contain("MSIPath");
        parameterNames.Should().Contain("TimestampServer");
        
        _output.WriteLine($"✓ Sign script parameters found: {string.Join(", ", parameterNames)}");
    }

    [Fact]
    public void BuildScript_ShouldValidateInputs()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var scriptContent = File.ReadAllText(buildScriptPath);

        // Assert
        scriptContent.Should().Contain("Test-Path");
        scriptContent.Should().Contain("if (");
        scriptContent.Should().Contain("throw");
        scriptContent.Should().Contain("not found");
        
        _output.WriteLine("✓ Build script contains input validation logic");
    }

    [Fact]
    public void SignScript_ShouldValidateInputs()
    {
        // Arrange
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var scriptContent = File.ReadAllText(signScriptPath);

        // Assert
        scriptContent.Should().Contain("Test-Path");
        scriptContent.Should().Contain("if (");
        scriptContent.Should().Contain("throw");
        scriptContent.Should().Contain("not found");
        
        _output.WriteLine("✓ Sign script contains input validation logic");
    }

    [Fact]
    public void BuildScript_ShouldHaveErrorHandling()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var scriptContent = File.ReadAllText(buildScriptPath);

        // Assert
        scriptContent.Should().Contain("try");
        scriptContent.Should().Contain("catch");
        scriptContent.Should().Contain("Write-Error");
        scriptContent.Should().Contain("exit 1");
        
        _output.WriteLine("✓ Build script has comprehensive error handling");
    }

    [Fact]
    public void SignScript_ShouldHaveErrorHandling()
    {
        // Arrange
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var scriptContent = File.ReadAllText(signScriptPath);

        // Assert
        scriptContent.Should().Contain("try");
        scriptContent.Should().Contain("catch");
        scriptContent.Should().Contain("Write-Error");
        scriptContent.Should().Contain("exit 1");
        
        _output.WriteLine("✓ Sign script has comprehensive error handling");
    }

    [Fact]
    public void BuildScript_ShouldHaveLogging()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var scriptContent = File.ReadAllText(buildScriptPath);

        // Assert
        scriptContent.Should().Contain("Write-Host");
        scriptContent.Should().Contain("Write-Information");
        scriptContent.Should().Contain("Write-Warning");
        scriptContent.Should().Contain("Write-Error");
        
        _output.WriteLine("✓ Build script has comprehensive logging");
    }

    [Fact]
    public void SignScript_ShouldHaveLogging()
    {
        // Arrange
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var scriptContent = File.ReadAllText(signScriptPath);

        // Assert
        scriptContent.Should().Contain("Write-Host");
        scriptContent.Should().Contain("Write-Information");
        scriptContent.Should().Contain("Write-Warning");
        scriptContent.Should().Contain("Write-Error");
        
        _output.WriteLine("✓ Sign script has comprehensive logging");
    }

    [Fact]
    public void BuildScript_ShouldHaveCorrectDotNetCommands()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var scriptContent = File.ReadAllText(buildScriptPath);

        // Assert
        scriptContent.Should().Contain("dotnet restore");
        scriptContent.Should().Contain("dotnet build");
        scriptContent.Should().Contain("dotnet publish");
        scriptContent.Should().Contain("--configuration");
        scriptContent.Should().Contain("--output");
        
        _output.WriteLine("✓ Build script has correct .NET CLI commands");
    }

    [Fact]
    public void BuildScript_ShouldHaveCorrectWixCommands()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var scriptContent = File.ReadAllText(buildScriptPath);

        // Assert
        scriptContent.Should().Contain("wix");
        scriptContent.Should().Contain("build");
        scriptContent.Should().Contain(".wixproj");
        scriptContent.Should().Contain("-o");
        
        _output.WriteLine("✓ Build script has correct WiX commands");
    }

    [Fact]
    public void SignScript_ShouldHaveCorrectSignToolCommand()
    {
        // Arrange
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var scriptContent = File.ReadAllText(signScriptPath);

        // Assert
        scriptContent.Should().Contain("signtool");
        scriptContent.Should().Contain("sign");
        scriptContent.Should().Contain("/f");
        scriptContent.Should().Contain("/p");
        scriptContent.Should().Contain("/t");
        scriptContent.Should().Contain("/d");
        scriptContent.Should().Contain("/du");
        
        _output.WriteLine("✓ Sign script has correct signtool command structure");
    }

    [Fact]
    public void Scripts_ShouldHaveCorrectFilePermissions()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var masterScriptPath = Path.Combine(_scriptsPath, "build-and-sign.ps1");

        // Act
        var buildScriptInfo = new FileInfo(buildScriptPath);
        var signScriptInfo = new FileInfo(signScriptPath);
        var masterScriptInfo = new FileInfo(masterScriptPath);

        // Assert
        buildScriptInfo.Exists.Should().BeTrue();
        signScriptInfo.Exists.Should().BeTrue();
        masterScriptInfo.Exists.Should().BeTrue();
        
        // Check if files are readable
        buildScriptInfo.IsReadOnly.Should().BeFalse();
        signScriptInfo.IsReadOnly.Should().BeFalse();
        masterScriptInfo.IsReadOnly.Should().BeFalse();
        
        _output.WriteLine("✓ All scripts have correct file permissions");
    }

    [Fact]
    public void Scripts_ShouldHaveCorrectEncoding()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var masterScriptPath = Path.Combine(_scriptsPath, "build-and-sign.ps1");

        // Act & Assert
        var buildContent = File.ReadAllText(buildScriptPath);
        var signContent = File.ReadAllText(signScriptPath);
        var masterContent = File.ReadAllText(masterScriptPath);

        buildContent.Should().NotContain("�"); // No encoding issues
        signContent.Should().NotContain("�"); // No encoding issues
        masterContent.Should().NotContain("�"); // No encoding issues
        
        _output.WriteLine("✓ All scripts have correct encoding (no garbled characters)");
    }

    [Fact]
    public void Scripts_ShouldHaveConsistentStyle()
    {
        // Arrange
        var buildScriptPath = Path.Combine(_scriptsPath, "build-installer.ps1");
        var signScriptPath = Path.Combine(_scriptsPath, "sign-installer.ps1");
        var masterScriptPath = Path.Combine(_scriptsPath, "build-and-sign.ps1");

        var buildContent = File.ReadAllText(buildScriptPath);
        var signContent = File.ReadAllText(signScriptPath);
        var masterContent = File.ReadAllText(masterScriptPath);

        // Assert consistent style patterns
        var scripts = new[] { buildContent, signContent, masterContent };
        var scriptNames = new[] { "build", "sign", "master" };

        for (int i = 0; i < scripts.Length; i++)
        {
            var script = scripts[i];
            var name = scriptNames[i];
            
            script.Should().Contain("param(", $"{name} script should have param block");
            script.Should().Contain("Write-Host", $"{name} script should have logging");
            script.Should().Contain("try", $"{name} script should have error handling");
            script.Should().Contain("catch", $"{name} script should have error handling");
            
            _output.WriteLine($"✓ {name} script has consistent style");
        }
    }

    [Fact]
    public void WixProject_ShouldExist()
    {
        // Arrange
        var wixProjectPath = Path.Combine(_projectRoot, "tools", "installer", "EAM.Installer.wixproj");
        var productWxsPath = Path.Combine(_projectRoot, "tools", "installer", "Product.wxs");
        var featuresWxsPath = Path.Combine(_projectRoot, "tools", "installer", "Features.wxs");

        // Act & Assert
        File.Exists(wixProjectPath).Should().BeTrue($"WiX project should exist at {wixProjectPath}");
        File.Exists(productWxsPath).Should().BeTrue($"Product.wxs should exist at {productWxsPath}");
        File.Exists(featuresWxsPath).Should().BeTrue($"Features.wxs should exist at {featuresWxsPath}");
        
        _output.WriteLine("✓ WiX project and source files exist");
    }

    [Fact]
    public void WixProject_ShouldHaveCorrectStructure()
    {
        // Arrange
        var wixProjectPath = Path.Combine(_projectRoot, "tools", "installer", "EAM.Installer.wixproj");
        var projectContent = File.ReadAllText(wixProjectPath);

        // Assert
        projectContent.Should().Contain("<Project");
        projectContent.Should().Contain("Wix.Sdk");
        projectContent.Should().Contain("Product.wxs");
        projectContent.Should().Contain("Features.wxs");
        
        _output.WriteLine("✓ WiX project has correct structure");
    }
}