# Employee Activity Monitor (EAM) v5.0 - Scripts de Build e CI/CD

*Documento gerado em: 18 jul 2025*

## Scripts de Build

### 1. Build Script Principal (build.ps1)

```powershell
#!/usr/bin/env pwsh
# EAM Build Script v5.0
# Builds all components of the Employee Activity Monitor solution

param(
    [string]$Configuration = "Release",
    [string]$Platform = "Any CPU",
    [switch]$SkipTests,
    [switch]$SkipWeb,
    [switch]$Clean,
    [string]$OutputPath = "./dist"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Employee Activity Monitor (EAM) v5.0 Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Green
Write-Host "Platform: $Platform" -ForegroundColor Green
Write-Host "Output Path: $OutputPath" -ForegroundColor Green

# Verificar pré-requisitos
Write-Host "`n--- Verificando Pré-requisitos ---" -ForegroundColor Yellow

# Verificar .NET 8 SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Error "❌ .NET 8 SDK não encontrado. Instale a partir de https://dot.net"
    exit 1
}

# Verificar Node.js (se não pular web)
if (-not $SkipWeb) {
    try {
        $nodeVersion = node --version
        Write-Host "✓ Node.js: $nodeVersion" -ForegroundColor Green
    } catch {
        Write-Error "❌ Node.js não encontrado. Instale a partir de https://nodejs.org"
        exit 1
    }
}

# Criar diretório de output
if (Test-Path $OutputPath) {
    if ($Clean) {
        Remove-Item -Recurse -Force $OutputPath
        Write-Host "✓ Diretório de output limpo" -ForegroundColor Green
    }
} else {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
    Write-Host "✓ Diretório de output criado: $OutputPath" -ForegroundColor Green
}

# Limpar solution se necessário
if ($Clean) {
    Write-Host "`n--- Limpando Solution ---" -ForegroundColor Yellow
    dotnet clean EAM.sln --configuration $Configuration --verbosity minimal
    Write-Host "✓ Solution limpa" -ForegroundColor Green
}

# Restaurar dependências
Write-Host "`n--- Restaurando Dependências ---" -ForegroundColor Yellow
dotnet restore EAM.sln --verbosity minimal
Write-Host "✓ Dependências restauradas" -ForegroundColor Green

# Build da solution .NET
Write-Host "`n--- Building .NET Solution ---" -ForegroundColor Yellow
dotnet build EAM.sln --configuration $Configuration --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Falha no build da solution .NET"
    exit 1
}
Write-Host "✓ .NET Solution compilada com sucesso" -ForegroundColor Green

# Executar testes
if (-not $SkipTests) {
    Write-Host "`n--- Executando Testes ---" -ForegroundColor Yellow
    dotnet test EAM.sln --configuration $Configuration --no-build --verbosity minimal --logger trx --results-directory "$OutputPath/TestResults"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Falha nos testes"
        exit 1
    }
    Write-Host "✓ Todos os testes passaram" -ForegroundColor Green
}

# Publicar EAM.Agent
Write-Host "`n--- Publicando EAM.Agent ---" -ForegroundColor Yellow
dotnet publish src/EAM.Agent/EAM.Agent.csproj --configuration $Configuration --output "$OutputPath/Agent" --runtime win-x64 --self-contained true --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Falha na publicação do Agent"
    exit 1
}
Write-Host "✓ EAM.Agent publicado em: $OutputPath/Agent" -ForegroundColor Green

# Publicar EAM.API
Write-Host "`n--- Publicando EAM.API ---" -ForegroundColor Yellow
dotnet publish src/EAM.API/EAM.API.csproj --configuration $Configuration --output "$OutputPath/API" --runtime win-x64 --self-contained false --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Falha na publicação da API"
    exit 1
}
Write-Host "✓ EAM.API publicada em: $OutputPath/API" -ForegroundColor Green

# Build do Frontend Angular
if (-not $SkipWeb) {
    Write-Host "`n--- Building Frontend Angular ---" -ForegroundColor Yellow
    
    Push-Location src/EAM.Web
    try {
        # Instalar dependências
        npm ci
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Falha na instalação das dependências do Angular"
            exit 1
        }
        
        # Build produção
        npm run build:prod
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Falha no build do Angular"
            exit 1
        }
        
        # Copiar artifacts para output
        Copy-Item -Recurse -Force "./dist/eam-web/*" "../../$OutputPath/Web/"
        Write-Host "✓ Frontend Angular compilado e copiado para: $OutputPath/Web" -ForegroundColor Green
    } finally {
        Pop-Location
    }
}

# Criar arquivo de versão
Write-Host "`n--- Criando Arquivo de Versão ---" -ForegroundColor Yellow
$version = @{
    Version = "5.0.0"
    BuildDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Configuration = $Configuration
    Platform = $Platform
    GitCommit = if (Get-Command git -ErrorAction SilentlyContinue) { git rev-parse --short HEAD } else { "N/A" }
} | ConvertTo-Json -Depth 2

$version | Out-File "$OutputPath/version.json" -Encoding UTF8
Write-Host "✓ Arquivo de versão criado: $OutputPath/version.json" -ForegroundColor Green

Write-Host "`n=== Build Concluído com Sucesso! ===" -ForegroundColor Cyan
Write-Host "Artifacts disponíveis em: $OutputPath" -ForegroundColor Green
```

### 2. Script de Build Docker (build-docker.ps1)

```powershell
#!/usr/bin/env pwsh
# EAM Docker Build Script v5.0

param(
    [string]$Registry = "eam-registry",
    [string]$Tag = "latest",
    [switch]$Push,
    [switch]$NoBuildCache
)

$ErrorActionPreference = "Stop"

Write-Host "=== EAM Docker Build Script ===" -ForegroundColor Cyan

$images = @(
    @{ Name = "eam-agent"; Path = "src/EAM.Agent"; Dockerfile = "Dockerfile" },
    @{ Name = "eam-api"; Path = "src/EAM.API"; Dockerfile = "Dockerfile" },
    @{ Name = "eam-web"; Path = "src/EAM.Web"; Dockerfile = "Dockerfile" }
)

foreach ($image in $images) {
    $imageName = "$Registry/$($image.Name):$Tag"
    $buildArgs = @("build", "-t", $imageName, "-f", "$($image.Path)/$($image.Dockerfile)", ".")
    
    if ($NoBuildCache) {
        $buildArgs += "--no-cache"
    }
    
    Write-Host "`n--- Building $imageName ---" -ForegroundColor Yellow
    
    & docker @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Falha no build da imagem $imageName"
        exit 1
    }
    
    Write-Host "✓ Imagem $imageName construída com sucesso" -ForegroundColor Green
    
    if ($Push) {
        Write-Host "--- Pushing $imageName ---" -ForegroundColor Yellow
        docker push $imageName
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Falha no push da imagem $imageName"
            exit 1
        }
        Write-Host "✓ Imagem $imageName enviada com sucesso" -ForegroundColor Green
    }
}

Write-Host "`n=== Docker Build Concluído! ===" -ForegroundColor Cyan
```

### 3. Script de Desenvolvimento (dev-setup.ps1)

```powershell
#!/usr/bin/env pwsh
# EAM Development Setup Script v5.0

param(
    [switch]$SkipDocker,
    [switch]$SkipDatabase,
    [switch]$SkipWeb
)

$ErrorActionPreference = "Stop"

Write-Host "=== EAM Development Setup ===" -ForegroundColor Cyan

# Verificar Docker
if (-not $SkipDocker) {
    Write-Host "`n--- Verificando Docker ---" -ForegroundColor Yellow
    try {
        docker --version | Out-Null
        Write-Host "✓ Docker encontrado" -ForegroundColor Green
    } catch {
        Write-Error "❌ Docker não encontrado. Instale o Docker Desktop"
        exit 1
    }
    
    # Subir infraestrutura
    Write-Host "--- Subindo Infraestrutura ---" -ForegroundColor Yellow
    docker-compose -f infrastructure/docker/docker-compose.dev.yml up -d
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Falha ao subir infraestrutura"
        exit 1
    }
    Write-Host "✓ Infraestrutura iniciada" -ForegroundColor Green
}

# Configurar banco de dados
if (-not $SkipDatabase) {
    Write-Host "`n--- Configurando Banco de Dados ---" -ForegroundColor Yellow
    
    # Aguardar PostgreSQL estar disponível
    $maxAttempts = 30
    $attempt = 0
    do {
        $attempt++
        try {
            $connection = New-Object System.Data.SqlClient.SqlConnection
            $connection.ConnectionString = "Host=localhost;Database=eam;Username=eam_user;Password=eam_pass;Port=5432"
            $connection.Open()
            $connection.Close()
            Write-Host "✓ PostgreSQL disponível" -ForegroundColor Green
            break
        } catch {
            if ($attempt -ge $maxAttempts) {
                Write-Error "❌ Timeout aguardando PostgreSQL"
                exit 1
            }
            Write-Host "Aguardando PostgreSQL... ($attempt/$maxAttempts)" -ForegroundColor Yellow
            Start-Sleep -Seconds 2
        }
    } while ($attempt -lt $maxAttempts)
    
    # Executar migrações
    Write-Host "--- Executando Migrações ---" -ForegroundColor Yellow
    Push-Location src/EAM.API
    try {
        dotnet ef database update --verbose
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Falha nas migrações"
            exit 1
        }
        Write-Host "✓ Migrações executadas" -ForegroundColor Green
    } finally {
        Pop-Location
    }
}

# Configurar frontend
if (-not $SkipWeb) {
    Write-Host "`n--- Configurando Frontend ---" -ForegroundColor Yellow
    Push-Location src/EAM.Web
    try {
        npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Falha na instalação das dependências do Angular"
            exit 1
        }
        Write-Host "✓ Dependências do Angular instaladas" -ForegroundColor Green
    } finally {
        Pop-Location
    }
}

Write-Host "`n=== Ambiente de Desenvolvimento Pronto! ===" -ForegroundColor Cyan
Write-Host "Serviços disponíveis:" -ForegroundColor Green
Write-Host "- PostgreSQL: localhost:5432" -ForegroundColor White
Write-Host "- Redis: localhost:6379" -ForegroundColor White
Write-Host "- MinIO: localhost:9000" -ForegroundColor White
Write-Host "- Grafana: localhost:3000" -ForegroundColor White
Write-Host "- Loki: localhost:3100" -ForegroundColor White
Write-Host "- Tempo: localhost:3200" -ForegroundColor White

Write-Host "`nPara iniciar o desenvolvimento:" -ForegroundColor Yellow
Write-Host "1. API: cd src/EAM.API && dotnet run" -ForegroundColor White
Write-Host "2. Web: cd src/EAM.Web && npm start" -ForegroundColor White
Write-Host "3. Agent: cd src/EAM.Agent && dotnet run" -ForegroundColor White
```

## Configuração de CI/CD

### 1. GitHub Actions (.github/workflows/ci.yml)

```yaml
name: EAM CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

env:
  DOTNET_VERSION: '8.0.x'
  NODE_VERSION: '18.x'
  REGISTRY: ghcr.io
  IMAGE_NAME: eam

jobs:
  # Job 1: Build e Test .NET
  build-dotnet:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Restore dependencies
      run: dotnet restore EAM.sln
    
    - name: Build
      run: dotnet build EAM.sln --configuration Release --no-restore
    
    - name: Test
      run: dotnet test EAM.sln --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage" --results-directory ./coverage
    
    - name: Upload coverage reports to Codecov
      uses: codecov/codecov-action@v3
      with:
        directory: ./coverage
        flags: dotnet
        name: codecov-dotnet
    
    - name: Publish Agent
      run: dotnet publish src/EAM.Agent/EAM.Agent.csproj --configuration Release --output ./dist/Agent --runtime win-x64 --self-contained true
    
    - name: Publish API
      run: dotnet publish src/EAM.API/EAM.API.csproj --configuration Release --output ./dist/API --runtime win-x64 --self-contained false
    
    - name: Upload .NET artifacts
      uses: actions/upload-artifact@v4
      with:
        name: dotnet-artifacts
        path: ./dist/
        retention-days: 7

  # Job 2: Build Angular
  build-angular:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup Node.js
      uses: actions/setup-node@v4
      with:
        node-version: ${{ env.NODE_VERSION }}
        cache: 'npm'
        cache-dependency-path: src/EAM.Web/package-lock.json
    
    - name: Install dependencies
      run: npm ci
      working-directory: src/EAM.Web
    
    - name: Lint
      run: npm run lint
      working-directory: src/EAM.Web
    
    - name: Test
      run: npm run test:ci
      working-directory: src/EAM.Web
    
    - name: Build
      run: npm run build:prod
      working-directory: src/EAM.Web
    
    - name: Upload Angular artifacts
      uses: actions/upload-artifact@v4
      with:
        name: angular-artifacts
        path: src/EAM.Web/dist/
        retention-days: 7

  # Job 3: Security Scan
  security-scan:
    runs-on: ubuntu-latest
    needs: [build-dotnet, build-angular]
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Run Trivy vulnerability scanner
      uses: aquasecurity/trivy-action@master
      with:
        scan-type: 'fs'
        scan-ref: '.'
        format: 'sarif'
        output: 'trivy-results.sarif'
    
    - name: Upload Trivy scan results to GitHub Security tab
      uses: github/codeql-action/upload-sarif@v2
      with:
        sarif_file: 'trivy-results.sarif'

  # Job 4: Build Docker Images
  build-docker:
    runs-on: ubuntu-latest
    needs: [build-dotnet, build-angular]
    if: github.ref == 'refs/heads/main'
    
    permissions:
      contents: read
      packages: write
    
    strategy:
      matrix:
        component: [agent, api, web]
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Download artifacts
      uses: actions/download-artifact@v4
      with:
        name: ${{ matrix.component == 'web' && 'angular-artifacts' || 'dotnet-artifacts' }}
        path: ./artifacts
    
    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3
    
    - name: Log in to Container Registry
      uses: docker/login-action@v3
      with:
        registry: ${{ env.REGISTRY }}
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}
    
    - name: Extract metadata
      id: meta
      uses: docker/metadata-action@v5
      with:
        images: ${{ env.REGISTRY }}/${{ github.repository }}-${{ matrix.component }}
        tags: |
          type=ref,event=branch
          type=ref,event=pr
          type=sha
          type=raw,value=latest,enable={{is_default_branch}}
    
    - name: Build and push Docker image
      uses: docker/build-push-action@v5
      with:
        context: .
        file: src/EAM.${{ matrix.component }}/Dockerfile
        push: true
        tags: ${{ steps.meta.outputs.tags }}
        labels: ${{ steps.meta.outputs.labels }}
        cache-from: type=gha
        cache-to: type=gha,mode=max

  # Job 5: Deploy to Staging
  deploy-staging:
    runs-on: ubuntu-latest
    needs: [build-docker, security-scan]
    if: github.ref == 'refs/heads/main'
    environment: staging
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Deploy to staging
      run: |
        echo "Deploying to staging environment..."
        # Aqui você adicionaria os comandos específicos para deploy
        # Por exemplo: kubectl apply, docker-compose, etc.
    
    - name: Run integration tests
      run: |
        echo "Running integration tests..."
        # Comandos para executar testes de integração
    
    - name: Notify deployment
      uses: 8398a7/action-slack@v3
      with:
        status: ${{ job.status }}
        channel: '#deployments'
        webhook_url: ${{ secrets.SLACK_WEBHOOK }}
      if: always()

  # Job 6: Deploy to Production
  deploy-production:
    runs-on: ubuntu-latest
    needs: [deploy-staging]
    if: github.ref == 'refs/heads/main'
    environment: production
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Deploy to production
      run: |
        echo "Deploying to production environment..."
        # Comandos específicos para deploy em produção
    
    - name: Run smoke tests
      run: |
        echo "Running smoke tests..."
        # Comandos para executar smoke tests
    
    - name: Notify deployment
      uses: 8398a7/action-slack@v3
      with:
        status: ${{ job.status }}
        channel: '#deployments'
        webhook_url: ${{ secrets.SLACK_WEBHOOK }}
      if: always()
```

### 2. Azure DevOps Pipeline (azure-pipelines.yml)

```yaml
# EAM Azure DevOps Pipeline
trigger:
  branches:
    include:
    - main
    - develop
  paths:
    include:
    - src/*
    - tests/*
    - infrastructure/*

pr:
  branches:
    include:
    - main
  paths:
    include:
    - src/*
    - tests/*

variables:
  buildConfiguration: 'Release'
  dotnetVersion: '8.0.x'
  nodeVersion: '18.x'
  vmImage: 'windows-latest'

stages:
- stage: Build
  displayName: 'Build and Test'
  jobs:
  - job: BuildDotNet
    displayName: 'Build .NET Components'
    pool:
      vmImage: $(vmImage)
    
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET SDK'
      inputs:
        packageType: 'sdk'
        version: $(dotnetVersion)
    
    - task: DotNetCoreCLI@2
      displayName: 'Restore packages'
      inputs:
        command: 'restore'
        projects: 'EAM.sln'
    
    - task: DotNetCoreCLI@2
      displayName: 'Build solution'
      inputs:
        command: 'build'
        projects: 'EAM.sln'
        arguments: '--configuration $(buildConfiguration) --no-restore'
    
    - task: DotNetCoreCLI@2
      displayName: 'Run tests'
      inputs:
        command: 'test'
        projects: 'tests/**/*.csproj'
        arguments: '--configuration $(buildConfiguration) --no-build --collect:"XPlat Code Coverage" --results-directory $(Agent.TempDirectory)'
    
    - task: PublishCodeCoverageResults@1
      displayName: 'Publish code coverage'
      inputs:
        codeCoverageTool: 'Cobertura'
        summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'
    
    - task: DotNetCoreCLI@2
      displayName: 'Publish Agent'
      inputs:
        command: 'publish'
        projects: 'src/EAM.Agent/EAM.Agent.csproj'
        arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)/Agent --runtime win-x64 --self-contained true'
    
    - task: DotNetCoreCLI@2
      displayName: 'Publish API'
      inputs:
        command: 'publish'
        projects: 'src/EAM.API/EAM.API.csproj'
        arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)/API --runtime win-x64 --self-contained false'
    
    - task: PublishBuildArtifacts@1
      displayName: 'Publish .NET artifacts'
      inputs:
        pathtoPublish: '$(Build.ArtifactStagingDirectory)'
        artifactName: 'dotnet-artifacts'

  - job: BuildAngular
    displayName: 'Build Angular Application'
    pool:
      vmImage: 'ubuntu-latest'
    
    steps:
    - task: NodeTool@0
      displayName: 'Use Node.js'
      inputs:
        versionSpec: $(nodeVersion)
    
    - task: Npm@1
      displayName: 'npm ci'
      inputs:
        command: 'ci'
        workingDir: 'src/EAM.Web'
    
    - task: Npm@1
      displayName: 'npm run lint'
      inputs:
        command: 'custom'
        customCommand: 'run lint'
        workingDir: 'src/EAM.Web'
    
    - task: Npm@1
      displayName: 'npm run test'
      inputs:
        command: 'custom'
        customCommand: 'run test:ci'
        workingDir: 'src/EAM.Web'
    
    - task: Npm@1
      displayName: 'npm run build'
      inputs:
        command: 'custom'
        customCommand: 'run build:prod'
        workingDir: 'src/EAM.Web'
    
    - task: PublishBuildArtifacts@1
      displayName: 'Publish Angular artifacts'
      inputs:
        pathtoPublish: 'src/EAM.Web/dist'
        artifactName: 'angular-artifacts'

- stage: Deploy
  displayName: 'Deploy to Staging'
  dependsOn: Build
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
  jobs:
  - deployment: DeployStaging
    displayName: 'Deploy to Staging Environment'
    environment: 'staging'
    pool:
      vmImage: $(vmImage)
    strategy:
      runOnce:
        deploy:
          steps:
          - task: DownloadBuildArtifacts@0
            displayName: 'Download artifacts'
            inputs:
              buildType: 'current'
              downloadType: 'specific'
              downloadPath: '$(System.ArtifactsDirectory)'
          
          - task: PowerShell@2
            displayName: 'Deploy to staging'
            inputs:
              targetType: 'inline'
              script: |
                Write-Host "Deploying EAM to staging environment..."
                # Adicionar comandos específicos de deploy
          
          - task: PowerShell@2
            displayName: 'Run integration tests'
            inputs:
              targetType: 'inline'
              script: |
                Write-Host "Running integration tests..."
                # Adicionar comandos para testes de integração
```

### 3. Dockerfile Templates

#### EAM.Agent Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/EAM.Agent/EAM.Agent.csproj", "src/EAM.Agent/"]
COPY ["src/EAM.Shared/EAM.Shared.csproj", "src/EAM.Shared/"]
RUN dotnet restore "src/EAM.Agent/EAM.Agent.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/src/EAM.Agent"
RUN dotnet build "EAM.Agent.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "EAM.Agent.csproj" -c Release -o /app/publish --runtime win-x64 --self-contained true

# Runtime stage
FROM mcr.microsoft.com/windows/servercore:ltsc2022
WORKDIR /app
COPY --from=publish /app/publish .

# Install .NET Runtime (if not self-contained)
# RUN powershell -Command "iex ((New-Object System.Net.WebClient).DownloadString('https://dot.net/v1/dotnet-install.ps1')); .\dotnet-install.ps1 -Runtime windowsdesktop -Version 8.0.0"

# Create plugins directory
RUN mkdir C:\ProgramData\EAM\plugins

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD powershell -Command "try { Get-Process -Name 'EAM.Agent' -ErrorAction Stop; exit 0 } catch { exit 1 }"

ENTRYPOINT ["EAM.Agent.exe"]
```

#### EAM.API Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/EAM.API/EAM.API.csproj", "src/EAM.API/"]
COPY ["src/EAM.Shared/EAM.Shared.csproj", "src/EAM.Shared/"]
RUN dotnet restore "src/EAM.API/EAM.API.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/src/EAM.API"
RUN dotnet build "EAM.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "EAM.API.csproj" -c Release -o /app/publish --runtime linux-x64 --self-contained false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "EAM.API.dll"]
```

#### EAM.Web Dockerfile

```dockerfile
# Build stage
FROM node:18-alpine AS build
WORKDIR /app

# Copy package files
COPY src/EAM.Web/package*.json ./
RUN npm ci --only=production

# Copy source code and build
COPY src/EAM.Web/ ./
RUN npm run build:prod

# Runtime stage
FROM nginx:alpine
COPY --from=build /app/dist/eam-web /usr/share/nginx/html
COPY src/EAM.Web/nginx.conf /etc/nginx/nginx.conf

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:80/health || exit 1

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### 4. Kubernetes Deployment Templates

#### EAM Namespace

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: eam-system
  labels:
    name: eam-system
    app.kubernetes.io/name: eam
    app.kubernetes.io/version: "5.0.0"
```

#### EAM API Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: eam-api
  namespace: eam-system
  labels:
    app: eam-api
    version: "5.0.0"
spec:
  replicas: 3
  selector:
    matchLabels:
      app: eam-api
  template:
    metadata:
      labels:
        app: eam-api
        version: "5.0.0"
    spec:
      containers:
      - name: eam-api
        image: eam-registry/eam-api:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: eam-secrets
              key: database-connection
        - name: JWT__Secret
          valueFrom:
            secretKeyRef:
              name: eam-secrets
              key: jwt-secret
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: eam-api-service
  namespace: eam-system
spec:
  selector:
    app: eam-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: ClusterIP
```

## Scripts de Manutenção

### 1. Backup Script (backup.ps1)

```powershell
#!/usr/bin/env pwsh
# EAM Backup Script

param(
    [string]$BackupPath = "C:\Backups\EAM",
    [int]$RetentionDays = 30,
    [switch]$CompressBackup
)

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFolder = Join-Path $BackupPath $timestamp

Write-Host "=== EAM Backup Script ===" -ForegroundColor Cyan
Write-Host "Backup destination: $backupFolder" -ForegroundColor Green

# Create backup directory
New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null

# Backup database
Write-Host "Backing up PostgreSQL database..." -ForegroundColor Yellow
$pgDumpPath = "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe"
& $pgDumpPath -h localhost -U eam_user -d eam -f "$backupFolder\eam_database_$timestamp.sql"

# Backup MinIO data
Write-Host "Backing up MinIO data..." -ForegroundColor Yellow
# Use MinIO client to backup bucket
mc mirror minio/eam-screenshots "$backupFolder\screenshots"

# Backup configuration files
Write-Host "Backing up configuration files..." -ForegroundColor Yellow
Copy-Item -Path "C:\ProgramData\EAM\*" -Destination "$backupFolder\config" -Recurse -Force

# Compress backup if requested
if ($CompressBackup) {
    Write-Host "Compressing backup..." -ForegroundColor Yellow
    $archivePath = "$BackupPath\eam_backup_$timestamp.zip"
    Compress-Archive -Path $backupFolder -DestinationPath $archivePath -Force
    Remove-Item -Path $backupFolder -Recurse -Force
    Write-Host "Backup compressed to: $archivePath" -ForegroundColor Green
}

# Cleanup old backups
Write-Host "Cleaning up old backups..." -ForegroundColor Yellow
$cutoffDate = (Get-Date).AddDays(-$RetentionDays)
Get-ChildItem -Path $BackupPath -Directory | Where-Object { $_.CreationTime -lt $cutoffDate } | Remove-Item -Recurse -Force

Write-Host "=== Backup Complete ===" -ForegroundColor Cyan
```

### 2. Monitoring Script (monitor.ps1)

```powershell
#!/usr/bin/env pwsh
# EAM Monitoring Script

param(
    [int]$IntervalSeconds = 60,
    [string]$LogPath = "C:\Logs\EAM\monitor.log",
    [switch]$SendAlerts
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    Write-Host $logEntry
    $logEntry | Out-File -FilePath $LogPath -Append
}

function Test-ServiceHealth {
    param([string]$ServiceName)
    
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction Stop
        return $service.Status -eq "Running"
    } catch {
        return $false
    }
}

function Test-DatabaseConnection {
    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = "Host=localhost;Database=eam;Username=eam_user;Password=eam_pass;Port=5432"
        $connection.Open()
        $connection.Close()
        return $true
    } catch {
        return $false
    }
}

function Test-ApiHealth {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5000/health" -Method Get -TimeoutSec 10
        return $response.status -eq "Healthy"
    } catch {
        return $false
    }
}

Write-Log "Starting EAM monitoring..." "INFO"

while ($true) {
    $healthStatus = @{
        Timestamp = Get-Date
        AgentService = Test-ServiceHealth "EAM.Agent"
        Database = Test-DatabaseConnection
        API = Test-ApiHealth
    }
    
    $overallHealth = $healthStatus.AgentService -and $healthStatus.Database -and $healthStatus.API
    
    if ($overallHealth) {
        Write-Log "All systems healthy" "INFO"
    } else {
        $issues = @()
        if (-not $healthStatus.AgentService) { $issues += "Agent Service" }
        if (-not $healthStatus.Database) { $issues += "Database" }
        if (-not $healthStatus.API) { $issues += "API" }
        
        $message = "Health check failed: $($issues -join ', ')"
        Write-Log $message "ERROR"
        
        if ($SendAlerts) {
            # Send alert notification
            # Implementation depends on your alerting system
        }
    }
    
    Start-Sleep -Seconds $IntervalSeconds
}
```

Este conjunto de scripts fornece uma base sólida para build, deploy, e manutenção do EAM v5.0, cobrindo todos os aspectos do ciclo de vida do software.