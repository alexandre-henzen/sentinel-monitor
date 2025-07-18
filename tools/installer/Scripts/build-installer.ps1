<#
.SYNOPSIS
    Build script for EAM Agent MSI installer

.DESCRIPTION
    This script builds the EAM Agent MSI installer package using WiX Toolset.
    It handles the complete build process including:
    - Building the .NET application
    - Publishing the binaries
    - Creating the MSI package
    - Calculating checksums
    - Validating the installer

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER Platform
    Target platform (x64 or x86). Default is x64.

.PARAMETER Version
    Version number for the installer. If not specified, reads from project file.

.PARAMETER OutputPath
    Output path for the installer. Default is .\bin\Release\x64\

.PARAMETER Clean
    Clean build - removes all intermediate and output files before building.

.PARAMETER SkipTests
    Skip running tests before building the installer.

.PARAMETER SkipPublish
    Skip publishing the .NET application (assumes binaries are already published).

.PARAMETER Verbose
    Enable verbose output.

.EXAMPLE
    .\build-installer.ps1
    Build the installer with default settings

.EXAMPLE
    .\build-installer.ps1 -Configuration Debug -Platform x64 -Version "5.0.1"
    Build debug installer for x64 platform with specific version

.EXAMPLE
    .\build-installer.ps1 -Clean -Verbose
    Clean build with verbose output
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [ValidateSet("x64", "x86")]
    [string]$Platform = "x64",
    
    [string]$Version = "",
    
    [string]$OutputPath = "",
    
    [switch]$Clean,
    
    [switch]$SkipTests,
    
    [switch]$SkipPublish,
    
    [switch]$Verbose
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Enable verbose output if requested
if ($Verbose) {
    $VerbosePreference = "Continue"
}

# Script variables
$ScriptDir = $PSScriptRoot
$RootDir = Resolve-Path "$ScriptDir\..\..\.."
$SolutionFile = "$RootDir\EAM.sln"
$AgentProjectDir = "$RootDir\src\EAM.Agent"
$AgentProjectFile = "$AgentProjectDir\EAM.Agent.csproj"
$InstallerProjectDir = "$RootDir\tools\installer"
$InstallerProjectFile = "$InstallerProjectDir\EAM.Installer.wixproj"
$LogDir = "$RootDir\logs"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$LogFile = "$LogDir\installer_build_$Timestamp.log"

# Ensure log directory exists
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

# Function to write log messages
function Write-Log {
    param(
        [string]$Message,
        [ValidateSet("Info", "Warning", "Error", "Success")]
        [string]$Level = "Info"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    # Write to console with color
    switch ($Level) {
        "Info" { Write-Host $logMessage -ForegroundColor White }
        "Warning" { Write-Host $logMessage -ForegroundColor Yellow }
        "Error" { Write-Host $logMessage -ForegroundColor Red }
        "Success" { Write-Host $logMessage -ForegroundColor Green }
    }
    
    # Write to log file
    $logMessage | Out-File -FilePath $LogFile -Append -Encoding UTF8
}

# Function to execute command and log output
function Invoke-Command {
    param(
        [string]$Command,
        [string]$Arguments = "",
        [string]$WorkingDirectory = $PWD,
        [switch]$IgnoreErrors
    )
    
    Write-Log "Executing: $Command $Arguments" -Level "Info"
    
    try {
        if ($Arguments) {
            $result = & $Command $Arguments.Split(' ') 2>&1
        } else {
            $result = & $Command 2>&1
        }
        
        if ($result) {
            $result | ForEach-Object { Write-Log $_ -Level "Info" }
        }
        
        if ($LASTEXITCODE -ne 0 -and -not $IgnoreErrors) {
            throw "Command failed with exit code: $LASTEXITCODE"
        }
        
        return $result
    }
    catch {
        Write-Log "Command failed: $_" -Level "Error"
        if (-not $IgnoreErrors) {
            throw
        }
    }
}

# Function to get version from project file
function Get-ProjectVersion {
    param([string]$ProjectFile)
    
    try {
        $xml = [xml](Get-Content $ProjectFile)
        $versionNode = $xml.Project.PropertyGroup.Version
        if ($versionNode) {
            return $versionNode
        }
        
        $assemblyVersionNode = $xml.Project.PropertyGroup.AssemblyVersion
        if ($assemblyVersionNode) {
            return $assemblyVersionNode
        }
        
        return "1.0.0"
    }
    catch {
        Write-Log "Failed to read version from project file: $_" -Level "Warning"
        return "1.0.0"
    }
}

# Function to validate prerequisites
function Test-Prerequisites {
    Write-Log "Checking prerequisites..." -Level "Info"
    
    # Check if .NET SDK is installed
    try {
        $dotnetVersion = Invoke-Command "dotnet" "--version" -IgnoreErrors
        if (-not $dotnetVersion) {
            throw ".NET SDK not found"
        }
        Write-Log ".NET SDK version: $dotnetVersion" -Level "Success"
    }
    catch {
        Write-Log ".NET SDK is required but not found" -Level "Error"
        throw
    }
    
    # Check if WiX is installed
    try {
        $wixVersion = Invoke-Command "wix" "--version" -IgnoreErrors
        if (-not $wixVersion) {
            throw "WiX Toolset not found"
        }
        Write-Log "WiX Toolset version: $wixVersion" -Level "Success"
    }
    catch {
        Write-Log "WiX Toolset is required but not found" -Level "Error"
        throw
    }
    
    # Check if solution file exists
    if (-not (Test-Path $SolutionFile)) {
        Write-Log "Solution file not found: $SolutionFile" -Level "Error"
        throw
    }
    
    # Check if agent project file exists
    if (-not (Test-Path $AgentProjectFile)) {
        Write-Log "Agent project file not found: $AgentProjectFile" -Level "Error"
        throw
    }
    
    # Check if installer project file exists
    if (-not (Test-Path $InstallerProjectFile)) {
        Write-Log "Installer project file not found: $InstallerProjectFile" -Level "Error"
        throw
    }
    
    Write-Log "Prerequisites check passed" -Level "Success"
}

# Function to clean build artifacts
function Invoke-Clean {
    Write-Log "Cleaning build artifacts..." -Level "Info"
    
    try {
        # Clean .NET projects
        Invoke-Command "dotnet" "clean `"$SolutionFile`" --configuration $Configuration --verbosity minimal"
        
        # Clean installer project
        $installerOutputPath = "$InstallerProjectDir\bin"
        $installerObjPath = "$InstallerProjectDir\obj"
        
        if (Test-Path $installerOutputPath) {
            Remove-Item $installerOutputPath -Recurse -Force
            Write-Log "Removed installer output directory" -Level "Info"
        }
        
        if (Test-Path $installerObjPath) {
            Remove-Item $installerObjPath -Recurse -Force
            Write-Log "Removed installer obj directory" -Level "Info"
        }
        
        Write-Log "Clean completed" -Level "Success"
    }
    catch {
        Write-Log "Clean failed: $_" -Level "Error"
        throw
    }
}

# Function to build .NET solution
function Invoke-Build {
    Write-Log "Building .NET solution..." -Level "Info"
    
    try {
        Invoke-Command "dotnet" "build `"$SolutionFile`" --configuration $Configuration --verbosity minimal --no-restore"
        Write-Log "Build completed" -Level "Success"
    }
    catch {
        Write-Log "Build failed: $_" -Level "Error"
        throw
    }
}

# Function to run tests
function Invoke-Tests {
    Write-Log "Running tests..." -Level "Info"
    
    try {
        Invoke-Command "dotnet" "test `"$SolutionFile`" --configuration $Configuration --no-build --verbosity minimal"
        Write-Log "Tests completed" -Level "Success"
    }
    catch {
        Write-Log "Tests failed: $_" -Level "Error"
        throw
    }
}

# Function to publish .NET application
function Invoke-Publish {
    Write-Log "Publishing .NET application..." -Level "Info"
    
    try {
        $publishPath = "$AgentProjectDir\bin\$Configuration\net8.0\win-x64\publish"
        
        Invoke-Command "dotnet" "publish `"$AgentProjectFile`" --configuration $Configuration --runtime win-x64 --self-contained false --output `"$publishPath`" --verbosity minimal"
        
        # Verify published files
        $requiredFiles = @("EAM.Agent.exe", "EAM.Agent.dll", "EAM.Shared.dll", "appsettings.json")
        foreach ($file in $requiredFiles) {
            $filePath = "$publishPath\$file"
            if (-not (Test-Path $filePath)) {
                throw "Required file not found: $file"
            }
        }
        
        Write-Log "Publish completed to: $publishPath" -Level "Success"
        return $publishPath
    }
    catch {
        Write-Log "Publish failed: $_" -Level "Error"
        throw
    }
}

# Function to build MSI installer
function Invoke-BuildInstaller {
    param([string]$PublishPath)
    
    Write-Log "Building MSI installer..." -Level "Info"
    
    try {
        # Set output path if not specified
        if (-not $OutputPath) {
            $OutputPath = "$InstallerProjectDir\bin\$Configuration\$Platform"
        }
        
        # Ensure output directory exists
        if (-not (Test-Path $OutputPath)) {
            New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
        }
        
        # Build installer project
        $buildArgs = @(
            "build"
            "`"$InstallerProjectFile`""
            "--configuration $Configuration"
            "--arch $Platform"
            "--output `"$OutputPath`""
            "--verbosity minimal"
        )
        
        if ($Version) {
            $buildArgs += "--property ProductVersion=$Version"
        }
        
        Invoke-Command "dotnet" ($buildArgs -join " ")
        
        # Find the generated MSI file
        $msiFiles = Get-ChildItem -Path $OutputPath -Filter "*.msi" | Sort-Object LastWriteTime -Descending
        if ($msiFiles.Count -eq 0) {
            throw "No MSI file found in output directory"
        }
        
        $msiFile = $msiFiles[0].FullName
        Write-Log "MSI installer built: $msiFile" -Level "Success"
        
        return $msiFile
    }
    catch {
        Write-Log "MSI build failed: $_" -Level "Error"
        throw
    }
}

# Function to calculate file checksums
function Get-FileChecksum {
    param(
        [string]$FilePath,
        [string]$Algorithm = "SHA256"
    )
    
    try {
        $hash = Get-FileHash -Path $FilePath -Algorithm $Algorithm
        return $hash.Hash.ToLower()
    }
    catch {
        Write-Log "Failed to calculate checksum for $FilePath - Error: $($_.Exception.Message)" -Level "Error"
        return $null
    }
}

# Function to validate MSI installer
function Test-Installer {
    param([string]$MsiFile)
    
    Write-Log "Validating MSI installer..." -Level "Info"
    
    try {
        # Check if file exists and is not empty
        if (-not (Test-Path $MsiFile)) {
            throw "MSI file not found: $MsiFile"
        }
        
        $fileInfo = Get-Item $MsiFile
        if ($fileInfo.Length -eq 0) {
            throw "MSI file is empty"
        }
        
        Write-Log "MSI file size: $($fileInfo.Length) bytes" -Level "Info"
        
        # Calculate checksums
        $sha256 = Get-FileChecksum $MsiFile "SHA256"
        $sha1 = Get-FileChecksum $MsiFile "SHA1"
        $md5 = Get-FileChecksum $MsiFile "MD5"
        
        Write-Log "SHA256: $sha256" -Level "Info"
        Write-Log "SHA1: $sha1" -Level "Info"
        Write-Log "MD5: $md5" -Level "Info"
        
        # Save checksums to file
        $checksumFile = "$($MsiFile).checksums.txt"
        $checksumContent = @(
            "# EAM Agent Installer Checksums"
            "# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
            "# File: $(Split-Path $MsiFile -Leaf)"
            "# Size: $($fileInfo.Length) bytes"
            ""
            "SHA256: $sha256"
            "SHA1: $sha1"
            "MD5: $md5"
        )
        
        $checksumContent | Out-File -FilePath $checksumFile -Encoding UTF8
        Write-Log "Checksums saved to: $checksumFile" -Level "Success"
        
        # Validate MSI structure (basic check)
        try {
            # Try to read MSI properties using Windows Installer API
            $windowsInstaller = New-Object -ComObject WindowsInstaller.Installer
            $database = $windowsInstaller.GetType().InvokeMember("OpenDatabase", "InvokeMethod", $null, $windowsInstaller, @($MsiFile, 0))
            
            if ($database) {
                Write-Log "MSI structure validation passed" -Level "Success"
                [System.Runtime.Interopservices.Marshal]::ReleaseComObject($database) | Out-Null
            }
            
            [System.Runtime.Interopservices.Marshal]::ReleaseComObject($windowsInstaller) | Out-Null
        }
        catch {
            Write-Log "MSI structure validation failed: $_" -Level "Warning"
        }
        
        Write-Log "MSI validation completed" -Level "Success"
        
        return @{
            FilePath = $MsiFile
            Size = $fileInfo.Length
            SHA256 = $sha256
            SHA1 = $sha1
            MD5 = $md5
            ChecksumFile = $checksumFile
        }
    }
    catch {
        Write-Log "MSI validation failed: $_" -Level "Error"
        throw
    }
}

# Function to create build summary
function New-BuildSummary {
    param(
        [string]$MsiFile,
        [hashtable]$ValidationResult,
        [datetime]$StartTime,
        [datetime]$EndTime
    )
    
    $duration = $EndTime - $StartTime
    $summaryFile = "$LogDir\build_summary_$Timestamp.json"
    
    $summary = @{
        BuildInfo = @{
            Timestamp = $StartTime.ToString("yyyy-MM-dd HH:mm:ss")
            Duration = $duration.ToString()
            Configuration = $Configuration
            Platform = $Platform
            Version = $Version
            Success = $true
        }
        OutputFiles = @{
            MsiFile = $MsiFile
            LogFile = $LogFile
            ChecksumFile = $ValidationResult.ChecksumFile
        }
        Validation = $ValidationResult
        Environment = @{
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            OSVersion = [System.Environment]::OSVersion.ToString()
            MachineName = [System.Environment]::MachineName
            UserName = [System.Environment]::UserName
        }
    }
    
    $summary | ConvertTo-Json -Depth 3 | Out-File -FilePath $summaryFile -Encoding UTF8
    Write-Log "Build summary saved to: $summaryFile" -Level "Success"
}

# Main execution
try {
    $startTime = Get-Date
    Write-Log "Starting EAM Agent installer build..." -Level "Info"
    Write-Log "Configuration: $Configuration" -Level "Info"
    Write-Log "Platform: $Platform" -Level "Info"
    Write-Log "Log file: $LogFile" -Level "Info"
    
    # Validate prerequisites
    Test-Prerequisites
    
    # Get version if not specified
    if (-not $Version) {
        $Version = Get-ProjectVersion $AgentProjectFile
        Write-Log "Detected version: $Version" -Level "Info"
    }
    
    # Clean if requested
    if ($Clean) {
        Invoke-Clean
    }
    
    # Restore packages
    Write-Log "Restoring NuGet packages..." -Level "Info"
    Invoke-Command "dotnet" "restore `"$SolutionFile`" --verbosity minimal"
    
    # Build solution
    Invoke-Build
    
    # Run tests if not skipped
    if (-not $SkipTests) {
        Invoke-Tests
    }
    
    # Publish application if not skipped
    $publishPath = $null
    if (-not $SkipPublish) {
        $publishPath = Invoke-Publish
    }
    
    # Build installer
    $msiFile = Invoke-BuildInstaller $publishPath
    
    # Validate installer
    $validationResult = Test-Installer $msiFile
    
    # Create build summary
    $endTime = Get-Date
    New-BuildSummary $msiFile $validationResult $startTime $endTime
    
    Write-Log "Build completed successfully!" -Level "Success"
    Write-Log "MSI file: $msiFile" -Level "Success"
    Write-Log "Build duration: $($endTime - $startTime)" -Level "Info"
    
    # Return success
    exit 0
}
catch {
    Write-Log "Build failed: $_" -Level "Error"
    Write-Log "Check the log file for details: $LogFile" -Level "Error"
    exit 1
}