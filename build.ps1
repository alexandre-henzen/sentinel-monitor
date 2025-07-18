#!/usr/bin/env pwsh
# EAM Build Script v5.0
# Builds Employee Activity Monitor solution

param(
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$Clean,
    [string]$OutputPath = "./dist"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Employee Activity Monitor (EAM) v5.0 Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Green
Write-Host "Output Path: $OutputPath" -ForegroundColor Green

# Verificar .NET 8 SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET 8 SDK não encontrado. Instale o .NET 8 SDK." -ForegroundColor Red
    exit 1
}

# Clean se solicitado
if ($Clean) {
    Write-Host "`n--- Limpando Solution ---" -ForegroundColor Yellow
    dotnet clean EAM.sln --configuration $Configuration
    if (Test-Path $OutputPath) {
        Remove-Item -Path $OutputPath -Recurse -Force
    }
}

# Restore packages
Write-Host "`n--- Restaurando Pacotes ---" -ForegroundColor Yellow
dotnet restore EAM.sln

# Build solution
Write-Host "`n--- Compilando Solution ---" -ForegroundColor Yellow
dotnet build EAM.sln --configuration $Configuration --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Erro na compilação" -ForegroundColor Red
    exit 1
}

# Run tests (if not skipped)
if (-not $SkipTests) {
    Write-Host "`n--- Executando Testes ---" -ForegroundColor Yellow
    dotnet test EAM.sln --configuration $Configuration --no-build --verbosity normal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Testes falharam" -ForegroundColor Red
        exit 1
    }
}

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Publish Agent
Write-Host "`n--- Publicando EAM.Agent ---" -ForegroundColor Yellow
dotnet publish src/EAM.Agent/EAM.Agent.csproj `
    --configuration $Configuration `
    --output "$OutputPath/Agent" `
    --runtime win-x64 `
    --self-contained true `
    --no-restore

# Publish API
Write-Host "`n--- Publicando EAM.API ---" -ForegroundColor Yellow
dotnet publish src/EAM.API/EAM.API.csproj `
    --configuration $Configuration `
    --output "$OutputPath/API" `
    --no-restore

# Publish PluginSDK
Write-Host "`n--- Publicando EAM.PluginSDK ---" -ForegroundColor Yellow
dotnet pack tools/EAM.PluginSDK/EAM.PluginSDK.csproj `
    --configuration $Configuration `
    --output "$OutputPath/SDK" `
    --no-restore

Write-Host "`n=== Build Completado com Sucesso ===" -ForegroundColor Green
Write-Host "Arquivos de saída em: $OutputPath" -ForegroundColor Green
Write-Host "- Agent: $OutputPath/Agent/" -ForegroundColor Gray
Write-Host "- API: $OutputPath/API/" -ForegroundColor Gray
Write-Host "- SDK: $OutputPath/SDK/" -ForegroundColor Gray

# Verificar arquivos principais
$agentExe = "$OutputPath/Agent/EAM.Agent.exe"
$apiDll = "$OutputPath/API/EAM.API.dll"

if ((Test-Path $agentExe) -and (Test-Path $apiDll)) {
    Write-Host "`n✓ Todos os componentes principais foram compilados com sucesso!" -ForegroundColor Green
} else {
    Write-Host "`n✗ Alguns componentes podem estar faltando." -ForegroundColor Yellow
}