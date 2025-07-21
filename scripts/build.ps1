#!/usr/bin/env pwsh

# EAM Build Script
# This script builds the entire EAM solution with proper error handling and logging

param(
    [Parameter(HelpMessage="Build configuration (Debug/Release)")]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [Parameter(HelpMessage="Skip tests during build")]
    [switch]$SkipTests,
    
    [Parameter(HelpMessage="Clean before build")]
    [switch]$Clean,
    
    [Parameter(HelpMessage="Restore packages before build")]
    [switch]$Restore,
    
    [Parameter(HelpMessage="Build specific project")]
    [string]$Project = "",
    
    [Parameter(HelpMessage="Verbose output")]
    [switch]$Verbose
)

# Set error handling
$ErrorActionPreference = "Stop"

# Colors for output
$Red = [System.ConsoleColor]::Red
$Green = [System.ConsoleColor]::Green
$Yellow = [System.ConsoleColor]::Yellow
$Blue = [System.ConsoleColor]::Blue
$White = [System.ConsoleColor]::White

function Write-ColorOutput {
    param(
        [string]$Message,
        [System.ConsoleColor]$Color = $White
    )
    $currentColor = $Host.UI.RawUI.ForegroundColor
    $Host.UI.RawUI.ForegroundColor = $Color
    Write-Output $Message
    $Host.UI.RawUI.ForegroundColor = $currentColor
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "🔄 $Message" $Blue
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✅ $Message" $Green
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "❌ $Message" $Red
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "⚠️ $Message" $Yellow
}

# Main build function
function Build-EAM {
    try {
        $startTime = Get-Date
        Write-ColorOutput "🚀 Starting EAM Build Process" $Green
        Write-ColorOutput "Configuration: $Configuration" $White
        Write-ColorOutput "Timestamp: $startTime" $White
        Write-Output ""

        # Check if we're in the correct directory
        if (-not (Test-Path "EAM.sln")) {
            throw "EAM.sln not found. Please run this script from the repository root."
        }

        # Clean if requested
        if ($Clean) {
            Write-Step "Cleaning solution..."
            dotnet clean EAM.sln --configuration $Configuration --verbosity minimal
            if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
            Write-Success "Solution cleaned successfully"
        }

        # Restore packages if requested
        if ($Restore) {
            Write-Step "Restoring NuGet packages..."
            dotnet restore EAM.sln --verbosity minimal
            if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }
            Write-Success "Packages restored successfully"
        }

        # Build solution or specific project
        if ($Project) {
            Write-Step "Building project: $Project"
            $buildArgs = @("build", $Project, "--configuration", $Configuration, "--no-restore")
        } else {
            Write-Step "Building entire solution..."
            $buildArgs = @("build", "EAM.sln", "--configuration", $Configuration, "--no-restore")
        }

        if ($Verbose) {
            $buildArgs += "--verbosity", "detailed"
        } else {
            $buildArgs += "--verbosity", "minimal"
        }

        & dotnet $buildArgs
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        Write-Success "Build completed successfully"

        # Run tests if not skipped
        if (-not $SkipTests) {
            Write-Step "Running tests..."
            dotnet test EAM.sln --configuration $Configuration --no-build --verbosity minimal
            if ($LASTEXITCODE -ne 0) { 
                Write-Warning "Some tests failed, but build completed"
            } else {
                Write-Success "All tests passed"
            }
        }

        # Build Angular application
        if (Test-Path "src/Web/EAM.Web/ClientApp") {
            Write-Step "Building Angular application..."
            Push-Location "src/Web/EAM.Web/ClientApp"
            try {
                if (-not (Test-Path "node_modules")) {
                    Write-Step "Installing npm dependencies..."
                    npm install
                    if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
                }
                
                Write-Step "Building Angular app..."
                if ($Configuration -eq "Release") {
                    npm run build:prod
                } else {
                    npm run build
                }
                if ($LASTEXITCODE -ne 0) { throw "Angular build failed" }
                Write-Success "Angular build completed successfully"
            }
            finally {
                Pop-Location
            }
        }

        $endTime = Get-Date
        $duration = $endTime - $startTime
        Write-Output ""
        Write-ColorOutput "🎉 Build completed successfully!" $Green
        Write-ColorOutput "Total time: $($duration.ToString('mm\:ss'))" $White
        Write-ColorOutput "Configuration: $Configuration" $White
        
    } catch {
        Write-Error "Build failed: $($_.Exception.Message)"
        Write-Output ""
        Write-ColorOutput "🔍 Common solutions:" $Yellow
        Write-ColorOutput "  • Run with -Restore to restore packages" $White
        Write-ColorOutput "  • Run with -Clean to clean solution first" $White
        Write-ColorOutput "  • Check that .NET 8 SDK is installed" $White
        Write-ColorOutput "  • Check that Node.js 18+ is installed" $White
        exit 1
    }
}

# Show help if needed
if ($args -contains "-h" -or $args -contains "--help") {
    Write-Output "EAM Build Script"
    Write-Output ""
    Write-Output "Usage: ./scripts/build.ps1 [OPTIONS]"
    Write-Output ""
    Write-Output "Options:"
    Write-Output "  -Configuration <Debug|Release>  Build configuration (default: Debug)"
    Write-Output "  -SkipTests                     Skip running tests"
    Write-Output "  -Clean                         Clean before build"
    Write-Output "  -Restore                       Restore packages before build"
    Write-Output "  -Project <ProjectPath>         Build specific project only"
    Write-Output "  -Verbose                       Verbose build output"
    Write-Output "  -h, --help                     Show this help message"
    Write-Output ""
    Write-Output "Examples:"
    Write-Output "  ./scripts/build.ps1 -Configuration Release -Clean -Restore"
    Write-Output "  ./scripts/build.ps1 -Project src/API/EAM.API/EAM.API.csproj"
    Write-Output "  ./scripts/build.ps1 -SkipTests -Verbose"
    exit 0
}

# Run the build
Build-EAM