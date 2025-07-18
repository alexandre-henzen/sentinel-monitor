<#
.SYNOPSIS
    Master build and signing script for EAM Agent MSI installer

.DESCRIPTION
    This script orchestrates the complete build and signing process for the EAM Agent MSI installer.
    It calls the individual build and signing scripts in the correct order and handles error conditions.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER Version
    Version number for the installer. If not specified, reads from project file.

.PARAMETER CertificateFile
    Path to the certificate file for signing. Optional.

.PARAMETER CertificatePassword
    Password for the certificate file. Optional.

.PARAMETER Thumbprint
    Certificate thumbprint for signing. Optional.

.PARAMETER SkipSigning
    Skip the signing process.

.PARAMETER SkipTests
    Skip running tests before building.

.PARAMETER Clean
    Clean build - removes all intermediate and output files before building.

.PARAMETER Verbose
    Enable verbose output.

.EXAMPLE
    .\build-and-sign.ps1
    Build and sign with default settings

.EXAMPLE
    .\build-and-sign.ps1 -Version "5.0.1" -CertificateFile "cert.pfx" -Clean
    Clean build with specific version and certificate
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [string]$Version = "",
    
    [string]$CertificateFile = "",
    
    [SecureString]$CertificatePassword,
    
    [string]$Thumbprint = "",
    
    [switch]$SkipSigning,
    
    [switch]$SkipTests,
    
    [switch]$Clean,
    
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
$BuildScript = "$ScriptDir\build-installer.ps1"
$SignScript = "$ScriptDir\sign-installer.ps1"
$RootDir = Resolve-Path "$ScriptDir\..\..\.."
$LogDir = "$RootDir\logs"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$MasterLogFile = "$LogDir\master_build_$Timestamp.log"

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
        "Info" { Write-Host $logMessage -ForegroundColor Cyan }
        "Warning" { Write-Host $logMessage -ForegroundColor Yellow }
        "Error" { Write-Host $logMessage -ForegroundColor Red }
        "Success" { Write-Host $logMessage -ForegroundColor Green }
    }
    
    # Write to log file
    $logMessage | Out-File -FilePath $MasterLogFile -Append -Encoding UTF8
}

# Function to execute PowerShell script
function Invoke-Script {
    param(
        [string]$ScriptPath,
        [hashtable]$Parameters = @{}
    )
    
    Write-Log "Executing script: $ScriptPath" -Level "Info"
    
    try {
        # Build parameter string
        $paramString = ""
        foreach ($key in $Parameters.Keys) {
            $value = $Parameters[$key]
            if ($value -is [switch] -and $value) {
                $paramString += " -$key"
            } elseif ($value -is [SecureString]) {
                $paramString += " -$key `$securePassword"
            } elseif ($value -and $value -ne "") {
                $paramString += " -$key '$value'"
            }
        }
        
        Write-Log "Parameters: $paramString" -Level "Info"
        
        # Execute script
        if ($Parameters.ContainsKey("CertificatePassword") -and $Parameters["CertificatePassword"]) {
            $securePassword = $Parameters["CertificatePassword"]
            $result = & $ScriptPath @Parameters
        } else {
            $result = & $ScriptPath @Parameters
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "Script failed with exit code: $LASTEXITCODE"
        }
        
        Write-Log "Script completed successfully" -Level "Success"
        return $result
    }
    catch {
        Write-Log "Script failed: $($_.Exception.Message)" -Level "Error"
        throw
    }
}

# Function to validate prerequisites
function Test-Prerequisites {
    Write-Log "Validating prerequisites..." -Level "Info"
    
    # Check if build script exists
    if (-not (Test-Path $BuildScript)) {
        throw "Build script not found: $BuildScript"
    }
    
    # Check if signing script exists (only if not skipping signing)
    if (-not $SkipSigning -and -not (Test-Path $SignScript)) {
        throw "Signing script not found: $SignScript"
    }
    
    # Check if certificate is provided when not skipping signing
    if (-not $SkipSigning -and -not $CertificateFile -and -not $Thumbprint) {
        Write-Log "No certificate specified. Signing will be skipped." -Level "Warning"
        $script:SkipSigning = $true
    }
    
    Write-Log "Prerequisites validated" -Level "Success"
}

# Function to create final summary
function New-FinalSummary {
    param(
        [string]$MsiFile,
        [bool]$BuildSuccess,
        [bool]$SigningSuccess,
        [datetime]$StartTime,
        [datetime]$EndTime
    )
    
    $duration = $EndTime - $StartTime
    $summaryFile = "$LogDir\final_summary_$Timestamp.json"
    
    $summary = @{
        Process = @{
            Timestamp = $StartTime.ToString("yyyy-MM-dd HH:mm:ss")
            Duration = $duration.ToString()
            Configuration = $Configuration
            Version = $Version
            BuildSuccess = $BuildSuccess
            SigningSuccess = $SigningSuccess
            OverallSuccess = $BuildSuccess -and ($SkipSigning -or $SigningSuccess)
        }
        Files = @{
            MsiFile = $MsiFile
            MasterLogFile = $MasterLogFile
            BuildLogPattern = "$LogDir\installer_build_*.log"
            SigningLogPattern = "$LogDir\signing_*.log"
        }
        Configuration = @{
            Clean = $Clean.IsPresent
            SkipTests = $SkipTests.IsPresent
            SkipSigning = $SkipSigning.IsPresent
            Verbose = $Verbose.IsPresent
        }
        Environment = @{
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            OSVersion = [System.Environment]::OSVersion.ToString()
            MachineName = [System.Environment]::MachineName
            UserName = [System.Environment]::UserName
        }
    }
    
    $summary | ConvertTo-Json -Depth 3 | Out-File -FilePath $summaryFile -Encoding UTF8
    Write-Log "Final summary saved to: $summaryFile" -Level "Success"
    
    return $summary
}

# Main execution
try {
    $startTime = Get-Date
    Write-Log "=== EAM Agent MSI Build and Sign Process ===" -Level "Info"
    Write-Log "Start Time: $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))" -Level "Info"
    Write-Log "Configuration: $Configuration" -Level "Info"
    Write-Log "Version: $(if ($Version) { $Version } else { 'Auto-detect' })" -Level "Info"
    Write-Log "Skip Signing: $SkipSigning" -Level "Info"
    Write-Log "Skip Tests: $SkipTests" -Level "Info"
    Write-Log "Clean Build: $Clean" -Level "Info"
    Write-Log "Master Log: $MasterLogFile" -Level "Info"
    Write-Log "=========================================" -Level "Info"
    
    # Validate prerequisites
    Test-Prerequisites
    
    # Step 1: Build the installer
    Write-Log "STEP 1: Building MSI installer..." -Level "Info"
    
    $buildParams = @{
        Configuration = $Configuration
        Platform = "x64"
        SkipTests = $SkipTests
        Clean = $Clean
        Verbose = $Verbose
    }
    
    if ($Version) {
        $buildParams["Version"] = $Version
    }
    
    $buildResult = Invoke-Script $BuildScript $buildParams
    $buildSuccess = $LASTEXITCODE -eq 0
    
    if (-not $buildSuccess) {
        throw "Build process failed"
    }
    
    # Find the generated MSI file
    $installerDir = "$RootDir\tools\installer\bin\$Configuration\x64"
    $msiFiles = Get-ChildItem -Path $installerDir -Filter "*.msi" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    
    if ($msiFiles.Count -eq 0) {
        throw "No MSI file found after build"
    }
    
    $msiFile = $msiFiles[0].FullName
    Write-Log "MSI file generated: $msiFile" -Level "Success"
    
    # Step 2: Sign the installer (if not skipped)
    $signingSuccess = $true
    if (-not $SkipSigning) {
        Write-Log "STEP 2: Signing MSI installer..." -Level "Info"
        
        $signParams = @{
            MsiFile = $msiFile
            Verify = $true
            Verbose = $Verbose
        }
        
        if ($CertificateFile) {
            $signParams["CertificateFile"] = $CertificateFile
        }
        
        if ($CertificatePassword) {
            $signParams["CertificatePassword"] = $CertificatePassword
        }
        
        if ($Thumbprint) {
            $signParams["Thumbprint"] = $Thumbprint
        }
        
        if ($Version) {
            $signParams["ProductVersion"] = $Version
        }
        
        try {
            $signResult = Invoke-Script $SignScript $signParams
            $signingSuccess = $LASTEXITCODE -eq 0
            
            if ($signingSuccess) {
                Write-Log "MSI signing completed successfully" -Level "Success"
            } else {
                Write-Log "MSI signing failed, but continuing..." -Level "Warning"
            }
        }
        catch {
            Write-Log "MSI signing failed: $($_.Exception.Message)" -Level "Warning"
            $signingSuccess = $false
        }
    } else {
        Write-Log "STEP 2: Skipping MSI signing" -Level "Info"
    }
    
    # Step 3: Final validation and summary
    Write-Log "STEP 3: Final validation..." -Level "Info"
    
    if (-not (Test-Path $msiFile)) {
        throw "Final MSI file not found: $msiFile"
    }
    
    $msiInfo = Get-Item $msiFile
    Write-Log "Final MSI file: $msiFile" -Level "Success"
    Write-Log "File size: $($msiInfo.Length) bytes" -Level "Info"
    Write-Log "Last modified: $($msiInfo.LastWriteTime)" -Level "Info"
    
    # Create final summary
    $endTime = Get-Date
    $summary = New-FinalSummary $msiFile $buildSuccess $signingSuccess $startTime $endTime
    
    # Final status
    $overallSuccess = $buildSuccess -and ($SkipSigning -or $signingSuccess)
    
    Write-Log "=========================================" -Level "Info"
    Write-Log "Build Success: $buildSuccess" -Level $(if ($buildSuccess) { "Success" } else { "Error" })
    Write-Log "Signing Success: $(if ($SkipSigning) { 'Skipped' } else { $signingSuccess })" -Level $(if ($SkipSigning) { "Info" } elseif ($signingSuccess) { "Success" } else { "Warning" })
    Write-Log "Overall Success: $overallSuccess" -Level $(if ($overallSuccess) { "Success" } else { "Error" })
    Write-Log "Total Duration: $($endTime - $startTime)" -Level "Info"
    Write-Log "Final MSI: $msiFile" -Level "Success"
    Write-Log "=========================================" -Level "Info"
    
    if ($overallSuccess) {
        Write-Log "EAM Agent MSI build and sign process completed successfully!" -Level "Success"
        exit 0
    } else {
        Write-Log "EAM Agent MSI build and sign process completed with errors!" -Level "Error"
        exit 1
    }
}
catch {
    Write-Log "Build and sign process failed: $($_.Exception.Message)" -Level "Error"
    Write-Log "Check the logs for details:" -Level "Error"
    Write-Log "  Master Log: $MasterLogFile" -Level "Error"
    Write-Log "  Build Logs: $LogDir\installer_build_*.log" -Level "Error"
    Write-Log "  Signing Logs: $LogDir\signing_*.log" -Level "Error"
    exit 1
}