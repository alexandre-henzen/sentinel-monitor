<#
.SYNOPSIS
    Code signing script for EAM Agent MSI installer

.DESCRIPTION
    This script signs the EAM Agent MSI installer with digital certificates.
    It supports both development (self-signed) and production (commercial) certificates.
    The script also validates the signature and creates verification reports.

.PARAMETER MsiFile
    Path to the MSI file to sign. Required.

.PARAMETER CertificateFile
    Path to the certificate file (.pfx or .p12). Required unless using certificate store.

.PARAMETER CertificatePassword
    Password for the certificate file. Can be passed as SecureString.

.PARAMETER Thumbprint
    Certificate thumbprint to use from the certificate store.

.PARAMETER TimestampServer
    Timestamp server URL. Default is http://timestamp.digicert.com

.PARAMETER SignTool
    Path to signtool.exe. If not specified, searches in Windows SDK.

.PARAMETER Description
    Description for the signature. Default is "EAM Agent Installer".

.PARAMETER ProductName
    Product name for the signature. Default is "EAM Agent".

.PARAMETER ProductVersion
    Product version for the signature.

.PARAMETER CompanyName
    Company name for the signature. Default is "EAM Technologies".

.PARAMETER SupportUrl
    Support URL for the signature. Default is "https://support.eam.local".

.PARAMETER Verify
    Verify the signature after signing.

.PARAMETER Force
    Force signing even if file is already signed.

.PARAMETER Verbose
    Enable verbose output.

.EXAMPLE
    .\sign-installer.ps1 -MsiFile "EAM.Agent.v5.0.0.msi" -CertificateFile "cert.pfx" -CertificatePassword (ConvertTo-SecureString "password" -AsPlainText -Force)

.EXAMPLE
    .\sign-installer.ps1 -MsiFile "EAM.Agent.v5.0.0.msi" -Thumbprint "ABC123..." -Verify

.EXAMPLE
    .\sign-installer.ps1 -MsiFile "EAM.Agent.v5.0.0.msi" -CertificateFile "cert.pfx" -Force -Verbose
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$MsiFile,
    
    [string]$CertificateFile = "",
    
    [SecureString]$CertificatePassword,
    
    [string]$Thumbprint = "",
    
    [string]$TimestampServer = "http://timestamp.digicert.com",
    
    [string]$SignTool = "",
    
    [string]$Description = "EAM Agent Installer",
    
    [string]$ProductName = "EAM Agent",
    
    [string]$ProductVersion = "",
    
    [string]$CompanyName = "EAM Technologies",
    
    [string]$SupportUrl = "https://support.eam.local",
    
    [switch]$Verify,
    
    [switch]$Force,
    
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
$LogDir = "$RootDir\logs"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$LogFile = "$LogDir\signing_$Timestamp.log"

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

# Function to find signtool.exe
function Find-SignTool {
    Write-Log "Searching for signtool.exe..." -Level "Info"
    
    # If path is provided, use it
    if ($SignTool -and (Test-Path $SignTool)) {
        Write-Log "Using provided signtool: $SignTool" -Level "Info"
        return $SignTool
    }
    
    # Common locations for signtool.exe
    $signToolPaths = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
        "${env:ProgramFiles}\Windows Kits\10\bin\*\x64\signtool.exe",
        "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows\*\bin\signtool.exe",
        "${env:ProgramFiles}\Microsoft SDKs\Windows\*\bin\signtool.exe"
    )
    
    foreach ($path in $signToolPaths) {
        $found = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($found) {
            Write-Log "Found signtool: $($found.FullName)" -Level "Success"
            return $found.FullName
        }
    }
    
    # Try to find in PATH
    $signToolInPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($signToolInPath) {
        Write-Log "Found signtool in PATH: $($signToolInPath.Source)" -Level "Success"
        return $signToolInPath.Source
    }
    
    throw "signtool.exe not found. Please install Windows SDK or specify path with -SignTool parameter."
}

# Function to validate certificate
function Test-Certificate {
    param(
        [string]$CertPath,
        [SecureString]$Password
    )
    
    Write-Log "Validating certificate..." -Level "Info"
    
    try {
        if ($CertPath) {
            # Load certificate from file
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
            if ($Password) {
                $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
                $cert.Import($CertPath, $plainPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet)
            } else {
                $cert.Import($CertPath)
            }
            
            Write-Log "Certificate Subject: $($cert.Subject)" -Level "Info"
            Write-Log "Certificate Issuer: $($cert.Issuer)" -Level "Info"
            Write-Log "Certificate Valid From: $($cert.NotBefore)" -Level "Info"
            Write-Log "Certificate Valid To: $($cert.NotAfter)" -Level "Info"
            Write-Log "Certificate Thumbprint: $($cert.Thumbprint)" -Level "Info"
            
            # Check if certificate is valid
            if ($cert.NotAfter -lt (Get-Date)) {
                Write-Log "WARNING: Certificate has expired!" -Level "Warning"
            }
            
            if ($cert.NotBefore -gt (Get-Date)) {
                Write-Log "WARNING: Certificate is not yet valid!" -Level "Warning"
            }
            
            # Check if certificate has code signing capability
            $codeSigningUsage = $cert.Extensions | Where-Object { $_.Oid.FriendlyName -eq "Enhanced Key Usage" }
            if ($codeSigningUsage) {
                $usages = $codeSigningUsage.EnhancedKeyUsages
                $hasCodeSigning = $usages | Where-Object { $_.FriendlyName -eq "Code Signing" }
                if (-not $hasCodeSigning) {
                    Write-Log "WARNING: Certificate does not have Code Signing capability!" -Level "Warning"
                }
            }
            
            $cert.Dispose()
            Write-Log "Certificate validation completed" -Level "Success"
            return $true
        }
        elseif ($Thumbprint) {
            # Find certificate in store
            $cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $Thumbprint }
            if (-not $cert) {
                $cert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.Thumbprint -eq $Thumbprint }
            }
            
            if ($cert) {
                Write-Log "Found certificate in store: $($cert.Subject)" -Level "Success"
                return $true
            } else {
                throw "Certificate with thumbprint $Thumbprint not found in certificate store"
            }
        }
        else {
            throw "No certificate specified"
        }
    }
    catch {
        Write-Log "Certificate validation failed: $($_.Exception.Message)" -Level "Error"
        return $false
    }
}

# Function to check if file is already signed
function Test-FileSigned {
    param([string]$FilePath)
    
    try {
        $signature = Get-AuthenticodeSignature -FilePath $FilePath
        return $signature.Status -eq "Valid"
    }
    catch {
        return $false
    }
}

# Function to sign the MSI file
function Invoke-SignFile {
    param(
        [string]$FilePath,
        [string]$CertPath,
        [SecureString]$Password,
        [string]$CertThumbprint,
        [string]$SignToolPath
    )
    
    Write-Log "Signing file: $FilePath" -Level "Info"
    
    try {
        # Check if file is already signed
        if ((Test-FileSigned $FilePath) -and -not $Force) {
            Write-Log "File is already signed. Use -Force to override." -Level "Warning"
            return $false
        }
        
        # Build signtool arguments
        $signArgs = @("sign")
        
        # Certificate selection
        if ($CertPath) {
            $signArgs += "/f"
            $signArgs += "`"$CertPath`""
            
            if ($Password) {
                $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
                $signArgs += "/p"
                $signArgs += $plainPassword
            }
        }
        elseif ($CertThumbprint) {
            $signArgs += "/sha1"
            $signArgs += $CertThumbprint
            $signArgs += "/s"
            $signArgs += "My"
        }
        else {
            throw "No certificate specified"
        }
        
        # Timestamp server
        if ($TimestampServer) {
            $signArgs += "/t"
            $signArgs += $TimestampServer
        }
        
        # Description and product information
        if ($Description) {
            $signArgs += "/d"
            $signArgs += "`"$Description`""
        }
        
        if ($SupportUrl) {
            $signArgs += "/du"
            $signArgs += $SupportUrl
        }
        
        # Verbose mode
        if ($Verbose) {
            $signArgs += "/v"
        }
        
        # File to sign
        $signArgs += "`"$FilePath`""
        
        # Execute signtool
        Write-Log "Executing: $SignToolPath $($signArgs -join ' ')" -Level "Info"
        
        $process = Start-Process -FilePath $SignToolPath -ArgumentList $signArgs -Wait -NoNewWindow -PassThru -RedirectStandardOutput "$env:TEMP\signtool_output.txt" -RedirectStandardError "$env:TEMP\signtool_error.txt"
        
        # Read output
        $output = Get-Content "$env:TEMP\signtool_output.txt" -ErrorAction SilentlyContinue
        $errorOutput = Get-Content "$env:TEMP\signtool_error.txt" -ErrorAction SilentlyContinue
        
        # Clean up temp files
        Remove-Item "$env:TEMP\signtool_output.txt" -ErrorAction SilentlyContinue
        Remove-Item "$env:TEMP\signtool_error.txt" -ErrorAction SilentlyContinue
        
        # Log output
        if ($output) {
            $output | ForEach-Object { Write-Log "SignTool: $_" -Level "Info" }
        }
        
        if ($errorOutput) {
            $errorOutput | ForEach-Object { Write-Log "SignTool Error: $_" -Level "Warning" }
        }
        
        # Check exit code
        if ($process.ExitCode -eq 0) {
            Write-Log "File signed successfully" -Level "Success"
            return $true
        } else {
            Write-Log "Signing failed with exit code: $($process.ExitCode)" -Level "Error"
            return $false
        }
    }
    catch {
        Write-Log "Signing failed: $($_.Exception.Message)" -Level "Error"
        return $false
    }
}

# Function to verify signature
function Test-Signature {
    param(
        [string]$FilePath,
        [string]$SignToolPath
    )
    
    Write-Log "Verifying signature..." -Level "Info"
    
    try {
        # Use PowerShell's Get-AuthenticodeSignature
        $signature = Get-AuthenticodeSignature -FilePath $FilePath
        
        Write-Log "Signature Status: $($signature.Status)" -Level "Info"
        Write-Log "Signature StatusMessage: $($signature.StatusMessage)" -Level "Info"
        
        if ($signature.SignerCertificate) {
            Write-Log "Signer: $($signature.SignerCertificate.Subject)" -Level "Info"
            Write-Log "Issuer: $($signature.SignerCertificate.Issuer)" -Level "Info"
            Write-Log "Valid From: $($signature.SignerCertificate.NotBefore)" -Level "Info"
            Write-Log "Valid To: $($signature.SignerCertificate.NotAfter)" -Level "Info"
            Write-Log "Thumbprint: $($signature.SignerCertificate.Thumbprint)" -Level "Info"
            
            if ($signature.TimeStamperCertificate) {
                Write-Log "Timestamp: $($signature.TimeStamperCertificate.NotBefore)" -Level "Info"
                Write-Log "Timestamp Authority: $($signature.TimeStamperCertificate.Subject)" -Level "Info"
            }
        }
        
        # Use signtool for additional verification
        $verifyArgs = @("verify", "/v", "/pa", "`"$FilePath`"")
        
        Write-Log "Executing: $SignToolPath $($verifyArgs -join ' ')" -Level "Info"
        
        $process = Start-Process -FilePath $SignToolPath -ArgumentList $verifyArgs -Wait -NoNewWindow -PassThru -RedirectStandardOutput "$env:TEMP\signtool_verify_output.txt" -RedirectStandardError "$env:TEMP\signtool_verify_error.txt"
        
        # Read output
        $output = Get-Content "$env:TEMP\signtool_verify_output.txt" -ErrorAction SilentlyContinue
        $errorOutput = Get-Content "$env:TEMP\signtool_verify_error.txt" -ErrorAction SilentlyContinue
        
        # Clean up temp files
        Remove-Item "$env:TEMP\signtool_verify_output.txt" -ErrorAction SilentlyContinue
        Remove-Item "$env:TEMP\signtool_verify_error.txt" -ErrorAction SilentlyContinue
        
        # Log output
        if ($output) {
            $output | ForEach-Object { Write-Log "SignTool Verify: $_" -Level "Info" }
        }
        
        if ($errorOutput) {
            $errorOutput | ForEach-Object { Write-Log "SignTool Verify Error: $_" -Level "Warning" }
        }
        
        # Check results
        $isValid = ($signature.Status -eq "Valid") -and ($process.ExitCode -eq 0)
        
        if ($isValid) {
            Write-Log "Signature verification successful" -Level "Success"
        } else {
            Write-Log "Signature verification failed" -Level "Error"
        }
        
        return $isValid
    }
    catch {
        Write-Log "Signature verification failed: $($_.Exception.Message)" -Level "Error"
        return $false
    }
}

# Function to create signing report
function New-SigningReport {
    param(
        [string]$FilePath,
        [bool]$SigningSuccessful,
        [bool]$VerificationSuccessful
    )
    
    $reportFile = "$LogDir\signing_report_$Timestamp.json"
    
    $signature = Get-AuthenticodeSignature -FilePath $FilePath -ErrorAction SilentlyContinue
    
    $report = @{
        File = @{
            Path = $FilePath
            Name = Split-Path $FilePath -Leaf
            Size = (Get-Item $FilePath).Length
            LastModified = (Get-Item $FilePath).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        }
        Signing = @{
            Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
            Successful = $SigningSuccessful
            Tool = $SignTool
            TimestampServer = $TimestampServer
            Description = $Description
            CompanyName = $CompanyName
            SupportUrl = $SupportUrl
        }
        Verification = @{
            Successful = $VerificationSuccessful
            Status = if ($signature) { $signature.Status } else { "Unknown" }
            StatusMessage = if ($signature) { $signature.StatusMessage } else { "Unknown" }
        }
        Certificate = @{
            Subject = if ($signature -and $signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { "Unknown" }
            Issuer = if ($signature -and $signature.SignerCertificate) { $signature.SignerCertificate.Issuer } else { "Unknown" }
            Thumbprint = if ($signature -and $signature.SignerCertificate) { $signature.SignerCertificate.Thumbprint } else { "Unknown" }
            ValidFrom = if ($signature -and $signature.SignerCertificate) { $signature.SignerCertificate.NotBefore.ToString("yyyy-MM-dd HH:mm:ss") } else { "Unknown" }
            ValidTo = if ($signature -and $signature.SignerCertificate) { $signature.SignerCertificate.NotAfter.ToString("yyyy-MM-dd HH:mm:ss") } else { "Unknown" }
        }
        Environment = @{
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            OSVersion = [System.Environment]::OSVersion.ToString()
            MachineName = [System.Environment]::MachineName
            UserName = [System.Environment]::UserName
        }
    }
    
    $report | ConvertTo-Json -Depth 3 | Out-File -FilePath $reportFile -Encoding UTF8
    Write-Log "Signing report saved to: $reportFile" -Level "Success"
}

# Main execution
try {
    $startTime = Get-Date
    Write-Log "Starting MSI signing process..." -Level "Info"
    Write-Log "MSI File: $MsiFile" -Level "Info"
    Write-Log "Log File: $LogFile" -Level "Info"
    
    # Validate input file
    if (-not (Test-Path $MsiFile)) {
        throw "MSI file not found: $MsiFile"
    }
    
    # Get absolute path
    $MsiFile = Resolve-Path $MsiFile
    Write-Log "Absolute path: $MsiFile" -Level "Info"
    
    # Find signtool
    $SignToolPath = Find-SignTool
    
    # Validate certificate
    if (-not (Test-Certificate $CertificateFile $CertificatePassword)) {
        throw "Certificate validation failed"
    }
    
    # Sign the file
    $signingSuccessful = Invoke-SignFile $MsiFile $CertificateFile $CertificatePassword $Thumbprint $SignToolPath
    
    # Verify signature if requested or if signing was successful
    $verificationSuccessful = $false
    if ($Verify -or $signingSuccessful) {
        $verificationSuccessful = Test-Signature $MsiFile $SignToolPath
    }
    
    # Create signing report
    New-SigningReport $MsiFile $signingSuccessful $verificationSuccessful
    
    # Final status
    if ($signingSuccessful) {
        Write-Log "MSI signing completed successfully!" -Level "Success"
        if ($Verify -and $verificationSuccessful) {
            Write-Log "Signature verification passed!" -Level "Success"
        } elseif ($Verify) {
            Write-Log "Signature verification failed!" -Level "Error"
        }
    } else {
        Write-Log "MSI signing failed!" -Level "Error"
    }
    
    $endTime = Get-Date
    Write-Log "Process duration: $($endTime - $startTime)" -Level "Info"
    
    # Return appropriate exit code
    if ($signingSuccessful -and (-not $Verify -or $verificationSuccessful)) {
        exit 0
    } else {
        exit 1
    }
}
catch {
    Write-Log "Signing process failed: $($_.Exception.Message)" -Level "Error"
    Write-Log "Check the log file for details: $LogFile" -Level "Error"
    exit 1
}