<#
.SYNOPSIS
    Script de validação da API do sistema EAM v5.0
.DESCRIPTION
    Valida todos os endpoints e funcionalidades da API:
    - Endpoints REST
    - Autenticação JWT
    - Processamento NDJSON
    - Integração com banco de dados
    - Performance e disponibilidade
.PARAMETER Environment
    Ambiente de teste (Integration, Production)
.PARAMETER ApiUrl
    URL base da API (padrão: http://localhost:5000)
.EXAMPLE
    .\validate-api.ps1 -Environment Integration -ApiUrl http://localhost:5000
#>

param(
    [string]$Environment = "Integration",
    [string]$ApiUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Continue"

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

# Função para fazer requisições HTTP
function Invoke-APIRequest {
    param(
        [string]$Method = "GET",
        [string]$Endpoint,
        [object]$Body = $null,
        [hashtable]$Headers = @{},
        [string]$ContentType = "application/json"
    )
    
    $uri = "$ApiUrl$Endpoint"
    $requestParams = @{
        Uri = $uri
        Method = $Method
        Headers = $Headers
        ContentType = $ContentType
        UseBasicParsing = $true
    }
    
    if ($Body) {
        if ($ContentType -eq "application/json") {
            $requestParams.Body = $Body | ConvertTo-Json -Depth 10
        } else {
            $requestParams.Body = $Body
        }
    }
    
    try {
        $response = Invoke-RestMethod @requestParams
        return @{
            Success = $true
            StatusCode = 200
            Data = $response
            Error = $null
        }
    } catch {
        return @{
            Success = $false
            StatusCode = $_.Exception.Response.StatusCode.value__
            Data = $null
            Error = $_.Exception.Message
        }
    }
}

# Função para testar disponibilidade da API
function Test-APIAvailability {
    Write-Log "Testando disponibilidade da API..." "INFO"
    
    try {
        $response = Invoke-APIRequest -Endpoint "/health"
        
        if ($response.Success) {
            Write-Log "✓ API disponível em $ApiUrl" "SUCCESS"
            return $true
        } else {
            Write-Log "✗ API não disponível: $($response.Error)" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro ao testar disponibilidade: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar autenticação
function Test-Authentication {
    Write-Log "Testando autenticação..." "INFO"
    
    try {
        # Teste de login
        $loginData = @{
            username = "admin"
            password = "admin123"
        }
        
        $loginResponse = Invoke-APIRequest -Method "POST" -Endpoint "/api/auth/login" -Body $loginData
        
        if ($loginResponse.Success -and $loginResponse.Data.token) {
            Write-Log "✓ Autenticação bem-sucedida" "SUCCESS"
            
            # Teste de acesso com token
            $token = $loginResponse.Data.token
            $headers = @{ "Authorization" = "Bearer $token" }
            
            $protectedResponse = Invoke-APIRequest -Endpoint "/api/agents" -Headers $headers
            
            if ($protectedResponse.Success) {
                Write-Log "✓ Acesso autorizado com token JWT" "SUCCESS"
                return $true
            } else {
                Write-Log "✗ Falha no acesso autorizado" "ERROR"
                return $false
            }
        } else {
            Write-Log "✗ Falha na autenticação" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro no teste de autenticação: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar endpoints principais
function Test-MainEndpoints {
    Write-Log "Testando endpoints principais..." "INFO"
    
    $endpoints = @(
        @{ Method = "GET"; Path = "/health"; Description = "Health Check" },
        @{ Method = "GET"; Path = "/api/agents"; Description = "Listar Agentes" },
        @{ Method = "GET"; Path = "/api/events"; Description = "Listar Eventos" },
        @{ Method = "GET"; Path = "/api/dashboard"; Description = "Dashboard" },
        @{ Method = "GET"; Path = "/api/screenshots"; Description = "Listar Screenshots" }
    )
    
    $successCount = 0
    $totalCount = $endpoints.Count
    
    foreach ($endpoint in $endpoints) {
        $response = Invoke-APIRequest -Method $endpoint.Method -Endpoint $endpoint.Path
        
        if ($response.Success) {
            Write-Log "✓ $($endpoint.Description): $($endpoint.Method) $($endpoint.Path)" "SUCCESS"
            $successCount++
        } else {
            Write-Log "✗ $($endpoint.Description): $($endpoint.Method) $($endpoint.Path) - $($response.Error)" "ERROR"
        }
    }
    
    $successRate = ($successCount / $totalCount) * 100
    Write-Log "Endpoints testados: $successCount/$totalCount ($($successRate.ToString('F1'))%)" "INFO"
    
    return $successRate -ge 80
}

# Função para testar CRUD de agentes
function Test-AgentCRUD {
    Write-Log "Testando CRUD de agentes..." "INFO"
    
    try {
        # Criar agente
        $agentData = @{
            id = [System.Guid]::NewGuid().ToString()
            machineName = "TEST-MACHINE"
            username = "test-user"
            version = "5.0.0"
            status = "Online"
            lastHeartbeat = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        }
        
        $createResponse = Invoke-APIRequest -Method "POST" -Endpoint "/api/agents/register" -Body $agentData
        
        if ($createResponse.Success) {
            Write-Log "✓ Agente criado com sucesso" "SUCCESS"
            
            # Listar agentes
            $listResponse = Invoke-APIRequest -Endpoint "/api/agents"
            
            if ($listResponse.Success) {
                Write-Log "✓ Listagem de agentes bem-sucedida" "SUCCESS"
                
                # Atualizar agente (heartbeat)
                $heartbeatResponse = Invoke-APIRequest -Method "POST" -Endpoint "/api/agents/$($agentData.id)/heartbeat"
                
                if ($heartbeatResponse.Success) {
                    Write-Log "✓ Heartbeat de agente bem-sucedido" "SUCCESS"
                    return $true
                } else {
                    Write-Log "✗ Falha no heartbeat do agente" "ERROR"
                    return $false
                }
            } else {
                Write-Log "✗ Falha na listagem de agentes" "ERROR"
                return $false
            }
        } else {
            Write-Log "✗ Falha na criação do agente" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro no teste CRUD de agentes: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar processamento de eventos
function Test-EventProcessing {
    Write-Log "Testando processamento de eventos..." "INFO"
    
    try {
        # Criar evento
        $eventData = @{
            id = [System.Guid]::NewGuid().ToString()
            agentId = [System.Guid]::NewGuid().ToString()
            activityType = "WindowChange"
            processName = "test-process"
            windowTitle = "Test Window"
            timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            metadata = @{
                testEvent = $true
                validationScript = "validate-api.ps1"
            }
        }
        
        $eventResponse = Invoke-APIRequest -Method "POST" -Endpoint "/api/events" -Body $eventData
        
        if ($eventResponse.Success) {
            Write-Log "✓ Evento criado com sucesso" "SUCCESS"
            
            # Listar eventos
            $listResponse = Invoke-APIRequest -Endpoint "/api/events?limit=10"
            
            if ($listResponse.Success) {
                Write-Log "✓ Listagem de eventos bem-sucedida" "SUCCESS"
                return $true
            } else {
                Write-Log "✗ Falha na listagem de eventos" "ERROR"
                return $false
            }
        } else {
            Write-Log "✗ Falha na criação do evento" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro no teste de processamento de eventos: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar processamento NDJSON
function Test-NDJSONProcessing {
    Write-Log "Testando processamento NDJSON..." "INFO"
    
    try {
        # Preparar dados NDJSON
        $events = @()
        for ($i = 0; $i -lt 10; $i++) {
            $events += @{
                id = [System.Guid]::NewGuid().ToString()
                agentId = [System.Guid]::NewGuid().ToString()
                activityType = "KeyPress"
                processName = "ndjson-test"
                windowTitle = "NDJSON Test Window $i"
                timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                metadata = @{
                    batchIndex = $i
                    testBatch = $true
                }
            }
        }
        
        # Converter para NDJSON
        $ndjsonData = $events | ForEach-Object { $_ | ConvertTo-Json -Compress } | Join-String -Separator "`n"
        
        $response = Invoke-APIRequest -Method "POST" -Endpoint "/api/events/batch" -Body $ndjsonData -ContentType "application/x-ndjson"
        
        if ($response.Success) {
            Write-Log "✓ Processamento NDJSON bem-sucedido" "SUCCESS"
            return $true
        } else {
            Write-Log "✗ Falha no processamento NDJSON" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro no teste NDJSON: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar performance da API
function Test-APIPerformance {
    Write-Log "Testando performance da API..." "INFO"
    
    try {
        $performanceResults = @{}
        
        # Teste de latência
        $latencyTests = @()
        for ($i = 0; $i -lt 10; $i++) {
            $startTime = Get-Date
            $response = Invoke-APIRequest -Endpoint "/health"
            $endTime = Get-Date
            
            if ($response.Success) {
                $latencyTests += ($endTime - $startTime).TotalMilliseconds
            }
        }
        
        if ($latencyTests.Count -gt 0) {
            $avgLatency = ($latencyTests | Measure-Object -Average).Average
            $maxLatency = ($latencyTests | Measure-Object -Maximum).Maximum
            $minLatency = ($latencyTests | Measure-Object -Minimum).Minimum
            
            $performanceResults["Latência Média"] = $avgLatency
            $performanceResults["Latência Máxima"] = $maxLatency
            $performanceResults["Latência Mínima"] = $minLatency
            
            Write-Log "Performance - Latência média: $($avgLatency.ToString('F2'))ms" "INFO"
            Write-Log "Performance - Latência máxima: $($maxLatency.ToString('F2'))ms" "INFO"
            Write-Log "Performance - Latência mínima: $($minLatency.ToString('F2'))ms" "INFO"
        }
        
        # Teste de throughput
        $throughputStart = Get-Date
        $successfulRequests = 0
        
        for ($i = 0; $i -lt 100; $i++) {
            $response = Invoke-APIRequest -Endpoint "/health"
            if ($response.Success) {
                $successfulRequests++
            }
        }
        
        $throughputEnd = Get-Date
        $throughputDuration = ($throughputEnd - $throughputStart).TotalSeconds
        $requestsPerSecond = $successfulRequests / $throughputDuration
        
        $performanceResults["Throughput"] = $requestsPerSecond
        Write-Log "Performance - Throughput: $($requestsPerSecond.ToString('F2')) RPS" "INFO"
        
        # Critérios de performance
        $performanceOK = $true
        
        if ($avgLatency -gt 1000) {
            Write-Log "⚠ Latência alta: $($avgLatency.ToString('F2'))ms" "WARN"
            $performanceOK = $false
        }
        
        if ($requestsPerSecond -lt 10) {
            Write-Log "⚠ Throughput baixo: $($requestsPerSecond.ToString('F2')) RPS" "WARN"
            $performanceOK = $false
        }
        
        if ($performanceOK) {
            Write-Log "✓ Performance da API atende aos critérios" "SUCCESS"
        } else {
            Write-Log "✗ Performance da API abaixo dos critérios" "ERROR"
        }
        
        return $performanceOK
    } catch {
        Write-Log "✗ Erro no teste de performance: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar upload de screenshots
function Test-ScreenshotUpload {
    Write-Log "Testando upload de screenshots..." "INFO"
    
    try {
        # Gerar dados de imagem PNG simples
        $pngData = @(
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
            0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0x1D, 0x01, 0x01, 0x00, 0x00, 0xFF,
            0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
            0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        )
        
        $base64Data = [System.Convert]::ToBase64String($pngData)
        
        $screenshotData = @{
            id = [System.Guid]::NewGuid().ToString()
            agentId = [System.Guid]::NewGuid().ToString()
            timestamp = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            data = $base64Data
            format = "png"
            width = 1
            height = 1
            quality = 85
        }
        
        $response = Invoke-APIRequest -Method "POST" -Endpoint "/api/screenshots" -Body $screenshotData
        
        if ($response.Success) {
            Write-Log "✓ Upload de screenshot bem-sucedido" "SUCCESS"
            return $true
        } else {
            Write-Log "✗ Falha no upload de screenshot" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro no teste de upload de screenshot: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função principal de validação
function Invoke-APIValidation {
    Write-Log "=== Iniciando Validação da API ===" "INFO"
    Write-Log "Ambiente: $Environment" "INFO"
    Write-Log "URL da API: $ApiUrl" "INFO"
    
    $validationResults = @{}
    
    # 1. Disponibilidade
    $validationResults["Disponibilidade"] = Test-APIAvailability
    
    if (-not $validationResults["Disponibilidade"]) {
        Write-Log "API não está disponível. Abortando testes." "ERROR"
        return $false
    }
    
    # 2. Endpoints principais
    $validationResults["Endpoints Principais"] = Test-MainEndpoints
    
    # 3. Autenticação
    $validationResults["Autenticação"] = Test-Authentication
    
    # 4. CRUD de agentes
    $validationResults["CRUD Agentes"] = Test-AgentCRUD
    
    # 5. Processamento de eventos
    $validationResults["Processamento de Eventos"] = Test-EventProcessing
    
    # 6. Processamento NDJSON
    $validationResults["Processamento NDJSON"] = Test-NDJSONProcessing
    
    # 7. Upload de screenshots
    $validationResults["Upload Screenshots"] = Test-ScreenshotUpload
    
    # 8. Performance
    $validationResults["Performance"] = Test-APIPerformance
    
    # Resultado final
    $successCount = ($validationResults.Values | Where-Object { $_ -eq $true }).Count
    $totalCount = $validationResults.Count
    $successRate = ($successCount / $totalCount) * 100
    
    Write-Log "=== Resultado da Validação da API ===" "INFO"
    Write-Log "Aprovados: $successCount/$totalCount ($($successRate.ToString('F1'))%)" "INFO"
    
    foreach ($result in $validationResults.GetEnumerator()) {
        $status = if ($result.Value) { "✓" } else { "✗" }
        $level = if ($result.Value) { "SUCCESS" } else { "ERROR" }
        Write-Log "$status $($result.Key)" $level
    }
    
    if ($successRate -ge 90) {
        Write-Log "✅ API aprovada!" "SUCCESS"
        return $true
    } else {
        Write-Log "❌ API reprovada!" "ERROR"
        return $false
    }
}

# Executar validação
$result = Invoke-APIValidation
exit $(if ($result) { 0 } else { 1 })