<#
.SYNOPSIS
    Script de validação da infraestrutura do sistema EAM v5.0
.DESCRIPTION
    Valida todos os componentes de infraestrutura:
    - PostgreSQL
    - Redis
    - MinIO
    - Docker containers
    - Conectividade de rede
.PARAMETER Environment
    Ambiente de teste (Integration, Production)
.EXAMPLE
    .\validate-infrastructure.ps1 -Environment Integration
#>

param(
    [string]$Environment = "Integration"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InfrastructureDir = Join-Path (Split-Path -Parent (Split-Path -Parent $ScriptDir)) "infrastructure"

# Função de log
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARN" { "Yellow" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# Função para testar conexão PostgreSQL
function Test-PostgreSQLConnection {
    param([string]$ConnectionString)
    
    Write-Log "Testando conexão PostgreSQL..." "INFO"
    
    try {
        # Usar psql para testar conexão
        $testQuery = "SELECT version();"
        $result = docker exec eam-postgres psql -U postgres -d eam_test -c $testQuery 2>$null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "✓ PostgreSQL: Conectado com sucesso" "SUCCESS"
            return $true
        } else {
            Write-Log "✗ PostgreSQL: Falha na conexão" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ PostgreSQL: Erro - $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar conexão Redis
function Test-RedisConnection {
    Write-Log "Testando conexão Redis..." "INFO"
    
    try {
        $result = docker exec eam-redis redis-cli ping 2>$null
        
        if ($result -eq "PONG") {
            Write-Log "✓ Redis: Conectado com sucesso" "SUCCESS"
            return $true
        } else {
            Write-Log "✗ Redis: Falha na conexão" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Redis: Erro - $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar conexão MinIO
function Test-MinIOConnection {
    Write-Log "Testando conexão MinIO..." "INFO"
    
    try {
        $result = docker exec eam-minio mc admin info local 2>$null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "✓ MinIO: Conectado com sucesso" "SUCCESS"
            return $true
        } else {
            Write-Log "✗ MinIO: Falha na conexão" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ MinIO: Erro - $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para verificar containers Docker
function Test-DockerContainers {
    Write-Log "Verificando containers Docker..." "INFO"
    
    $requiredContainers = @(
        "eam-postgres",
        "eam-redis", 
        "eam-minio"
    )
    
    $allContainersRunning = $true
    
    foreach ($container in $requiredContainers) {
        try {
            $status = docker inspect --format='{{.State.Status}}' $container 2>$null
            
            if ($status -eq "running") {
                Write-Log "✓ Container ${container}: Executando" "SUCCESS"
            } else {
                Write-Log "✗ Container ${container}: Não executando (Status: $status)" "ERROR"
                $allContainersRunning = $false
            }
        } catch {
            Write-Log "✗ Container ${container}: Não encontrado" "ERROR"
            $allContainersRunning = $false
        }
    }
    
    return $allContainersRunning
}

# Função para testar performance da infraestrutura
function Test-InfrastructurePerformance {
    Write-Log "Testando performance da infraestrutura..." "INFO"
    
    $performanceResults = @{}
    
    # Teste PostgreSQL
    try {
        $startTime = Get-Date
        docker exec eam-postgres psql -U postgres -d eam_test -c "SELECT COUNT(*) FROM information_schema.tables;" > $null
        $pgTime = ((Get-Date) - $startTime).TotalMilliseconds
        $performanceResults["PostgreSQL"] = $pgTime
        
        if ($pgTime -lt 1000) {
            Write-Log "✓ PostgreSQL Performance: $($pgTime.ToString('F2'))ms" "SUCCESS"
        } else {
            Write-Log "⚠ PostgreSQL Performance: $($pgTime.ToString('F2'))ms (lento)" "WARN"
        }
    } catch {
        Write-Log "✗ PostgreSQL Performance: Erro no teste" "ERROR"
        $performanceResults["PostgreSQL"] = -1
    }
    
    # Teste Redis
    try {
        $startTime = Get-Date
        docker exec eam-redis redis-cli set test-key test-value > $null
        docker exec eam-redis redis-cli get test-key > $null
        docker exec eam-redis redis-cli del test-key > $null
        $redisTime = ((Get-Date) - $startTime).TotalMilliseconds
        $performanceResults["Redis"] = $redisTime
        
        if ($redisTime -lt 100) {
            Write-Log "✓ Redis Performance: $($redisTime.ToString('F2'))ms" "SUCCESS"
        } else {
            Write-Log "⚠ Redis Performance: $($redisTime.ToString('F2'))ms (lento)" "WARN"
        }
    } catch {
        Write-Log "✗ Redis Performance: Erro no teste" "ERROR"
        $performanceResults["Redis"] = -1
    }
    
    # Teste MinIO
    try {
        $testFile = "test-performance.txt"
        $testContent = "Performance test content"
        
        $startTime = Get-Date
        echo $testContent | docker exec -i eam-minio mc pipe local/test-bucket/$testFile > $null
        docker exec eam-minio mc rm local/test-bucket/$testFile > $null
        $minioTime = ((Get-Date) - $startTime).TotalMilliseconds
        $performanceResults["MinIO"] = $minioTime
        
        if ($minioTime -lt 500) {
            Write-Log "✓ MinIO Performance: $($minioTime.ToString('F2'))ms" "SUCCESS"
        } else {
            Write-Log "⚠ MinIO Performance: $($minioTime.ToString('F2'))ms (lento)" "WARN"
        }
    } catch {
        Write-Log "✗ MinIO Performance: Erro no teste" "ERROR"
        $performanceResults["MinIO"] = -1
    }
    
    return $performanceResults
}

# Função para verificar recursos do sistema
function Test-SystemResources {
    Write-Log "Verificando recursos do sistema..." "INFO"
    
    $resourcesOK = $true
    
    # Verificar memória disponível
    $availableMemory = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB
    if ($availableMemory -lt 2) {
        Write-Log "⚠ Memória disponível baixa: $($availableMemory.ToString('F1'))GB" "WARN"
        $resourcesOK = $false
    } else {
        Write-Log "✓ Memória disponível: $($availableMemory.ToString('F1'))GB" "SUCCESS"
    }
    
    # Verificar espaço em disco
    $diskSpace = (Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace / 1GB
    if ($diskSpace -lt 5) {
        Write-Log "⚠ Espaço em disco baixo: $($diskSpace.ToString('F1'))GB" "WARN"
        $resourcesOK = $false
    } else {
        Write-Log "✓ Espaço em disco: $($diskSpace.ToString('F1'))GB" "SUCCESS"
    }
    
    # Verificar CPU
    $cpuUsage = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
    if ($cpuUsage -gt 80) {
        Write-Log "⚠ CPU em uso alto: $cpuUsage%" "WARN"
        $resourcesOK = $false
    } else {
        Write-Log "✓ CPU em uso: $cpuUsage%" "SUCCESS"
    }
    
    return $resourcesOK
}

# Função para inicializar infraestrutura se necessário
function Initialize-Infrastructure {
    Write-Log "Inicializando infraestrutura..." "INFO"
    
    $composeFile = Join-Path $InfrastructureDir "docker-compose.yml"
    
    if (-not (Test-Path $composeFile)) {
        Write-Log "Arquivo docker-compose.yml não encontrado: $composeFile" "ERROR"
        return $false
    }
    
    try {
        # Verificar se Docker está rodando
        docker info > $null 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Log "Docker não está rodando" "ERROR"
            return $false
        }
        
        # Subir containers se necessário
        Set-Location $InfrastructureDir
        docker-compose up -d
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Infraestrutura inicializada com sucesso" "SUCCESS"
            Start-Sleep -Seconds 30  # Aguardar inicialização
            return $true
        } else {
            Write-Log "Falha ao inicializar infraestrutura" "ERROR"
            return $false
        }
    } catch {
        Write-Log "Erro ao inicializar infraestrutura: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para verificar logs de containers
function Test-ContainerLogs {
    Write-Log "Verificando logs dos containers..." "INFO"
    
    $containers = @("eam-postgres", "eam-redis", "eam-minio")
    $logsHealthy = $true
    
    foreach ($container in $containers) {
        try {
            $logs = docker logs --tail 20 $container 2>&1
            
            # Verificar por erros críticos nos logs
            $criticalErrors = @("ERROR", "FATAL", "PANIC", "failed", "connection refused")
            $hasErrors = $false
            
            foreach ($errorPattern in $criticalErrors) {
                if ($logs -match $errorPattern) {
                    $hasErrors = $true
                    break
                }
            }
            
            if ($hasErrors) {
                Write-Log "⚠ Container ${container}: Erros encontrados nos logs" "WARN"
                $logsHealthy = $false
            } else {
                Write-Log "✓ Container ${container}: Logs saudáveis" "SUCCESS"
            }
        } catch {
            Write-Log "✗ Container ${container}: Erro ao verificar logs" "ERROR"
            $logsHealthy = $false
        }
    }
    
    return $logsHealthy
}

# Função principal de validação
function Invoke-InfrastructureValidation {
    Write-Log "=== Iniciando Validação da Infraestrutura ===" "INFO"
    Write-Log "Ambiente: $Environment" "INFO"
    
    $validationResults = @{}
    
    # 1. Verificar recursos do sistema
    $validationResults["Recursos do Sistema"] = Test-SystemResources
    
    # 2. Inicializar infraestrutura
    $validationResults["Inicialização"] = Initialize-Infrastructure
    
    if (-not $validationResults["Inicialização"]) {
        Write-Log "Falha na inicialização da infraestrutura. Abortando validação." "ERROR"
        return $false
    }
    
    # 3. Verificar containers Docker
    $validationResults["Containers Docker"] = Test-DockerContainers
    
    # 4. Testar conexões
    $validationResults["Conexão PostgreSQL"] = Test-PostgreSQLConnection
    $validationResults["Conexão Redis"] = Test-RedisConnection
    $validationResults["Conexão MinIO"] = Test-MinIOConnection
    
    # 5. Testar performance
    $performanceResults = Test-InfrastructurePerformance
    $validationResults["Performance"] = $performanceResults.Values | ForEach-Object { $_ -gt 0 } | ForEach-Object { $_ -contains $false } | ForEach-Object { -not $_ }
    
    # 6. Verificar logs
    $validationResults["Logs dos Containers"] = Test-ContainerLogs
    
    # Resultado final
    $successCount = ($validationResults.Values | Where-Object { $_ -eq $true }).Count
    $totalCount = $validationResults.Count
    $successRate = ($successCount / $totalCount) * 100
    
    Write-Log "=== Resultado da Validação da Infraestrutura ===" "INFO"
    Write-Log "Aprovados: $successCount/$totalCount ($($successRate.ToString('F1'))%)" "INFO"
    
    foreach ($result in $validationResults.GetEnumerator()) {
        $status = if ($result.Value) { "✓" } else { "✗" }
        $level = if ($result.Value) { "SUCCESS" } else { "ERROR" }
        Write-Log "$status $($result.Key)" $level
    }
    
    if ($successRate -ge 90) {
        Write-Log "✅ Infraestrutura aprovada!" "SUCCESS"
        return $true
    } else {
        Write-Log "❌ Infraestrutura reprovada!" "ERROR"
        return $false
    }
}

# Executar validação
$result = Invoke-InfrastructureValidation
exit $(if ($result) { 0 } else { 1 })