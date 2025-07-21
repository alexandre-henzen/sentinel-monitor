#!/usr/bin/env pwsh

# EAM Development Setup Script
# This script sets up the development environment for EAM

param(
    [Parameter(HelpMessage="Force reinstall of all dependencies")]
    [switch]$Force,
    
    [Parameter(HelpMessage="Skip Docker setup")]
    [switch]$SkipDocker,
    
    [Parameter(HelpMessage="Skip Angular setup")]
    [switch]$SkipAngular,
    
    [Parameter(HelpMessage="Skip .NET setup")]
    [switch]$SkipDotNet,
    
    [Parameter(HelpMessage="Install development tools")]
    [switch]$InstallTools,
    
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

function Test-Command {
    param([string]$Command)
    try {
        Get-Command $Command -ErrorAction Stop | Out-Null
        return $true
    } catch {
        return $false
    }
}

function Setup-DotNet {
    if ($SkipDotNet) {
        Write-Warning "Skipping .NET setup"
        return
    }
    
    Write-Step "Setting up .NET environment..."
    
    # Check if .NET 8 SDK is installed
    if (-not (Test-Command "dotnet")) {
        Write-Error ".NET SDK not found. Please install .NET 8 SDK from https://dotnet.microsoft.com/download"
        return
    }
    
    # Check .NET version
    $dotnetVersion = dotnet --version
    Write-ColorOutput "Found .NET SDK version: $dotnetVersion" $White
    
    if (-not $dotnetVersion.StartsWith("8.")) {
        Write-Warning ".NET 8 SDK not found. Current version: $dotnetVersion"
        Write-Warning "Please install .NET 8 SDK from https://dotnet.microsoft.com/download"
    }
    
    # Install/update development tools
    if ($InstallTools) {
        Write-Step "Installing/updating .NET development tools..."
        dotnet tool install -g dotnet-ef --version 8.0.0
        dotnet tool install -g dotnet-aspnet-codegenerator --version 8.0.0
        dotnet tool install -g dotnet-format --version 5.1.250801
        dotnet tool install -g dotnet-reportgenerator-globaltool --version 5.1.26
        Write-Success ".NET tools installed/updated"
    }
    
    # Restore packages
    Write-Step "Restoring NuGet packages..."
    if (Test-Path "EAM.sln") {
        dotnet restore EAM.sln --verbosity minimal
        if ($LASTEXITCODE -eq 0) {
            Write-Success ".NET packages restored"
        } else {
            Write-Error ".NET package restore failed"
        }
    }
}

function Setup-Angular {
    if ($SkipAngular) {
        Write-Warning "Skipping Angular setup"
        return
    }
    
    Write-Step "Setting up Angular environment..."
    
    # Check if Node.js is installed
    if (-not (Test-Command "node")) {
        Write-Error "Node.js not found. Please install Node.js 18+ from https://nodejs.org"
        return
    }
    
    # Check Node.js version
    $nodeVersion = node --version
    Write-ColorOutput "Found Node.js version: $nodeVersion" $White
    
    $nodeVersionNumber = [version]($nodeVersion.TrimStart('v'))
    if ($nodeVersionNumber.Major -lt 18) {
        Write-Warning "Node.js 18+ is recommended. Current version: $nodeVersion"
    }
    
    # Check if npm is installed
    if (-not (Test-Command "npm")) {
        Write-Error "npm not found. Please install npm or use Node.js installer"
        return
    }
    
    # Install Angular CLI globally if not present
    if (-not (Test-Command "ng")) {
        Write-Step "Installing Angular CLI globally..."
        npm install -g @angular/cli@18
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Angular CLI installed"
        } else {
            Write-Error "Angular CLI installation failed"
        }
    }
    
    # Install Angular dependencies
    $angularPath = "src/Web/EAM.Web/ClientApp"
    if (Test-Path $angularPath) {
        Write-Step "Installing Angular dependencies..."
        Push-Location $angularPath
        try {
            if ($Force -and (Test-Path "node_modules")) {
                Remove-Item "node_modules" -Recurse -Force
            }
            
            npm install
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Angular dependencies installed"
            } else {
                Write-Error "Angular dependencies installation failed"
            }
        }
        finally {
            Pop-Location
        }
    }
}

function Setup-Docker {
    if ($SkipDocker) {
        Write-Warning "Skipping Docker setup"
        return
    }
    
    Write-Step "Checking Docker environment..."
    
    # Check if Docker is installed
    if (-not (Test-Command "docker")) {
        Write-Warning "Docker not found. Please install Docker Desktop from https://www.docker.com/products/docker-desktop"
        return
    }
    
    # Check if Docker is running
    try {
        docker info | Out-Null
        Write-Success "Docker is running"
    } catch {
        Write-Warning "Docker is not running. Please start Docker Desktop"
        return
    }
    
    # Check if docker-compose is available
    if (Test-Command "docker-compose") {
        Write-Success "Docker Compose is available"
    } else {
        Write-Warning "Docker Compose not found. Please install Docker Compose"
    }
}

function Setup-VSCode {
    Write-Step "Setting up VS Code configuration..."
    
    # Create .vscode directory if it doesn't exist
    if (-not (Test-Path ".vscode")) {
        New-Item -ItemType Directory -Path ".vscode" | Out-Null
    }
    
    # Create launch.json for debugging
    $launchJson = @'
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "EAM API",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build-api",
            "program": "${workspaceFolder}/src/API/EAM.API/bin/Debug/net8.0/EAM.API.dll",
            "args": [],
            "cwd": "${workspaceFolder}/src/API/EAM.API",
            "console": "internalConsole",
            "stopAtEntry": false,
            "env": {
                "ASPNETCORE_ENVIRONMENT": "Development"
            }
        },
        {
            "name": "EAM Agent",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build-agent",
            "program": "${workspaceFolder}/src/Agent/EAM.Agent/bin/Debug/net8.0/EAM.Agent.dll",
            "args": [],
            "cwd": "${workspaceFolder}/src/Agent/EAM.Agent",
            "console": "internalConsole",
            "stopAtEntry": false,
            "env": {
                "ASPNETCORE_ENVIRONMENT": "Development"
            }
        },
        {
            "name": "EAM Web",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build-web",
            "program": "${workspaceFolder}/src/Web/EAM.Web/bin/Debug/net8.0/EAM.Web.dll",
            "args": [],
            "cwd": "${workspaceFolder}/src/Web/EAM.Web",
            "console": "internalConsole",
            "stopAtEntry": false,
            "env": {
                "ASPNETCORE_ENVIRONMENT": "Development"
            }
        }
    ]
}
'@
    
    $launchJson | Out-File -FilePath ".vscode/launch.json" -Encoding UTF8
    
    # Create tasks.json for build tasks
    $tasksJson = @'
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build-api",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}/src/API/EAM.API/EAM.API.csproj",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "problemMatcher": "$msCompile"
        },
        {
            "label": "build-agent",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}/src/Agent/EAM.Agent/EAM.Agent.csproj",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "problemMatcher": "$msCompile"
        },
        {
            "label": "build-web",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}/src/Web/EAM.Web/EAM.Web.csproj",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "problemMatcher": "$msCompile"
        },
        {
            "label": "build-all",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}/EAM.sln"
            ],
            "group": "build",
            "problemMatcher": "$msCompile"
        },
        {
            "label": "test-all",
            "command": "dotnet",
            "type": "process",
            "args": [
                "test",
                "${workspaceFolder}/EAM.sln"
            ],
            "group": "test",
            "problemMatcher": "$msCompile"
        }
    ]
}
'@
    
    $tasksJson | Out-File -FilePath ".vscode/tasks.json" -Encoding UTF8
    
    Write-Success "VS Code configuration created"
}

function Main {
    try {
        $startTime = Get-Date
        Write-ColorOutput "🚀 EAM Development Environment Setup" $Green
        Write-ColorOutput "Timestamp: $startTime" $White
        Write-Output ""
        
        # Check if we're in the correct directory
        if (-not (Test-Path "EAM.sln")) {
            throw "EAM.sln not found. Please run this script from the repository root."
        }
        
        # Setup components
        Setup-DotNet
        Setup-Angular
        Setup-Docker
        Setup-VSCode
        
        $endTime = Get-Date
        $duration = $endTime - $startTime
        
        Write-Output ""
        Write-ColorOutput "🎉 Development environment setup completed!" $Green
        Write-ColorOutput "Total time: $($duration.ToString('mm\:ss'))" $White
        Write-Output ""
        Write-ColorOutput "📋 Next steps:" $Blue
        Write-ColorOutput "  1. Run './scripts/build.ps1' to build the solution" $White
        Write-ColorOutput "  2. Run 'docker-compose up' to start development services" $White
        Write-ColorOutput "  3. Open the project in VS Code for debugging" $White
        Write-Output ""
        
    } catch {
        Write-Error "Setup failed: $($_.Exception.Message)"
        exit 1
    }
}

# Show help if needed
if ($args -contains "-h" -or $args -contains "--help") {
    Write-Output "EAM Development Setup Script"
    Write-Output ""
    Write-Output "Usage: ./scripts/dev-setup.ps1 [OPTIONS]"
    Write-Output ""
    Write-Output "Options:"
    Write-Output "  -Force          Force reinstall of all dependencies"
    Write-Output "  -SkipDocker     Skip Docker setup"
    Write-Output "  -SkipAngular    Skip Angular setup"
    Write-Output "  -SkipDotNet     Skip .NET setup"
    Write-Output "  -InstallTools   Install development tools"
    Write-Output "  -Verbose        Verbose output"
    Write-Output "  -h, --help      Show this help message"
    Write-Output ""
    Write-Output "Examples:"
    Write-Output "  ./scripts/dev-setup.ps1 -InstallTools"
    Write-Output "  ./scripts/dev-setup.ps1 -Force -SkipDocker"
    Write-Output "  ./scripts/dev-setup.ps1 -SkipAngular -Verbose"
    exit 0
}

# Run the setup
Main