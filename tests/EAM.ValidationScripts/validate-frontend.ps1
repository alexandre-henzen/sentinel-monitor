<#
.SYNOPSIS
    Script de validação do frontend do sistema EAM v5.0
.DESCRIPTION
    Valida o frontend Angular com PrimeNG:
    - Disponibilidade da aplicação
    - Carregamento de páginas principais
    - Funcionalidades de navegação
    - Integração com API
    - Performance de carregamento
.PARAMETER Environment
    Ambiente de teste (Integration, Production)
.PARAMETER FrontendUrl
    URL do frontend (padrão: http://localhost:4200)
.EXAMPLE
    .\validate-frontend.ps1 -Environment Integration -FrontendUrl http://localhost:4200
#>

param(
    [string]$Environment = "Integration",
    [string]$FrontendUrl = "http://localhost:4200"
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

# Função para testar disponibilidade do frontend
function Test-FrontendAvailability {
    Write-Log "Testando disponibilidade do frontend..." "INFO"
    
    try {
        $response = Invoke-WebRequest -Uri $FrontendUrl -Method GET -UseBasicParsing -TimeoutSec 30
        
        if ($response.StatusCode -eq 200) {
            Write-Log "✓ Frontend disponível em $FrontendUrl" "SUCCESS"
            return $true
        } else {
            Write-Log "✗ Frontend não disponível - Status: $($response.StatusCode)" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro ao testar disponibilidade: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar carregamento de recursos
function Test-ResourceLoading {
    Write-Log "Testando carregamento de recursos..." "INFO"
    
    $resources = @(
        "/",
        "/assets/favicon.ico",
        "/assets/styles.css"
    )
    
    $successCount = 0
    $totalCount = $resources.Count
    
    foreach ($resource in $resources) {
        try {
            $uri = "$FrontendUrl$resource"
            $response = Invoke-WebRequest -Uri $uri -Method GET -UseBasicParsing -TimeoutSec 10
            
            if ($response.StatusCode -eq 200) {
                Write-Log "✓ Recurso carregado: $resource" "SUCCESS"
                $successCount++
            } else {
                Write-Log "✗ Falha ao carregar recurso: $resource - Status: $($response.StatusCode)" "ERROR"
            }
        } catch {
            Write-Log "✗ Erro ao carregar recurso: $resource - $($_.Exception.Message)" "ERROR"
        }
    }
    
    $successRate = ($successCount / $totalCount) * 100
    Write-Log "Recursos carregados: $successCount/$totalCount ($($successRate.ToString('F1'))%)" "INFO"
    
    return $successRate -ge 80
}

# Função para testar conteúdo HTML
function Test-HTMLContent {
    Write-Log "Testando conteúdo HTML..." "INFO"
    
    try {
        $response = Invoke-WebRequest -Uri $FrontendUrl -Method GET -UseBasicParsing -TimeoutSec 30
        $content = $response.Content
        
        $requiredElements = @(
            "<!DOCTYPE html>",
            "<html",
            "<head>",
            "<title>",
            "<body>",
            "app-root",
            "<script"
        )
        
        $foundElements = 0
        $totalElements = $requiredElements.Count
        
        foreach ($element in $requiredElements) {
            if ($content -like "*$element*") {
                Write-Log "✓ Elemento encontrado: $element" "SUCCESS"
                $foundElements++
            } else {
                Write-Log "✗ Elemento não encontrado: $element" "ERROR"
            }
        }
        
        # Verificar se Angular está presente
        if ($content -like "*angular*" -or $content -like "*ng-*") {
            Write-Log "✓ Angular detectado no HTML" "SUCCESS"
            $foundElements++
        } else {
            Write-Log "✗ Angular não detectado no HTML" "ERROR"
        }
        
        # Verificar se PrimeNG está presente
        if ($content -like "*primeng*" -or $content -like "*p-*") {
            Write-Log "✓ PrimeNG detectado no HTML" "SUCCESS"
            $foundElements++
        } else {
            Write-Log "⚠ PrimeNG não detectado no HTML (pode ser carregado dinamicamente)" "WARN"
        }
        
        $successRate = ($foundElements / ($totalElements + 2)) * 100
        Write-Log "Elementos HTML encontrados: $foundElements/$($totalElements + 2) ($($successRate.ToString('F1'))%)" "INFO"
        
        return $successRate -ge 80
    } catch {
        Write-Log "✗ Erro ao testar conteúdo HTML: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar performance de carregamento
function Test-LoadingPerformance {
    Write-Log "Testando performance de carregamento..." "INFO"
    
    try {
        $loadTimes = @()
        
        for ($i = 0; $i -lt 5; $i++) {
            $startTime = Get-Date
            $response = Invoke-WebRequest -Uri $FrontendUrl -Method GET -UseBasicParsing -TimeoutSec 30
            $endTime = Get-Date
            
            if ($response.StatusCode -eq 200) {
                $loadTime = ($endTime - $startTime).TotalMilliseconds
                $loadTimes += $loadTime
                Write-Log "Carregamento ${i}: $($loadTime.ToString('F2'))ms" "INFO"
            }
        }
        
        if ($loadTimes.Count -gt 0) {
            $avgLoadTime = ($loadTimes | Measure-Object -Average).Average
            $maxLoadTime = ($loadTimes | Measure-Object -Maximum).Maximum
            $minLoadTime = ($loadTimes | Measure-Object -Minimum).Minimum
            
            Write-Log "Performance - Tempo médio: $($avgLoadTime.ToString('F2'))ms" "INFO"
            Write-Log "Performance - Tempo máximo: $($maxLoadTime.ToString('F2'))ms" "INFO"
            Write-Log "Performance - Tempo mínimo: $($minLoadTime.ToString('F2'))ms" "INFO"
            
            # Critérios de performance
            if ($avgLoadTime -lt 3000) {
                Write-Log "✓ Performance de carregamento adequada" "SUCCESS"
                return $true
            } else {
                Write-Log "⚠ Performance de carregamento lenta: $($avgLoadTime.ToString('F2'))ms" "WARN"
                return $false
            }
        } else {
            Write-Log "✗ Nenhum teste de performance bem-sucedido" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro no teste de performance: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar conectividade com API
function Test-APIConnectivity {
    Write-Log "Testando conectividade com API..." "INFO"
    
    try {
        # Tentar acessar endpoints da API através do frontend
        $apiEndpoints = @(
            "http://localhost:5000/health",
            "http://localhost:5000/api/agents",
            "http://localhost:5000/api/events"
        )
        
        $successCount = 0
        $totalCount = $apiEndpoints.Count
        
        foreach ($endpoint in $apiEndpoints) {
            try {
                $response = Invoke-WebRequest -Uri $endpoint -Method GET -UseBasicParsing -TimeoutSec 10
                
                if ($response.StatusCode -eq 200) {
                    Write-Log "✓ API endpoint acessível: $endpoint" "SUCCESS"
                    $successCount++
                } else {
                    Write-Log "✗ API endpoint inacessível: $endpoint - Status: $($response.StatusCode)" "ERROR"
                }
            } catch {
                Write-Log "✗ Erro ao acessar API endpoint: $endpoint - $($_.Exception.Message)" "ERROR"
            }
        }
        
        $successRate = ($successCount / $totalCount) * 100
        Write-Log "Conectividade com API: $successCount/$totalCount ($($successRate.ToString('F1'))%)" "INFO"
        
        return $successRate -ge 80
    } catch {
        Write-Log "✗ Erro no teste de conectividade com API: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar segurança HTTP
function Test-HTTPSecurity {
    Write-Log "Testando segurança HTTP..." "INFO"
    
    try {
        $response = Invoke-WebRequest -Uri $FrontendUrl -Method GET -UseBasicParsing -TimeoutSec 30
        
        $securityHeaders = @{
            "X-Content-Type-Options" = "nosniff"
            "X-Frame-Options" = "DENY"
            "X-XSS-Protection" = "1; mode=block"
            "Strict-Transport-Security" = $null
            "Content-Security-Policy" = $null
        }
        
        $securityScore = 0
        $totalHeaders = $securityHeaders.Count
        
        foreach ($header in $securityHeaders.GetEnumerator()) {
            if ($response.Headers.ContainsKey($header.Key)) {
                Write-Log "✓ Cabeçalho de segurança presente: $($header.Key)" "SUCCESS"
                $securityScore++
            } else {
                Write-Log "⚠ Cabeçalho de segurança ausente: $($header.Key)" "WARN"
            }
        }
        
        # Verificar se está usando HTTPS em produção
        if ($Environment -eq "Production" -and $FrontendUrl -like "http://*") {
            Write-Log "⚠ Produção deveria usar HTTPS" "WARN"
        } else {
            Write-Log "✓ Protocolo adequado para o ambiente" "SUCCESS"
        }
        
        $securityRate = ($securityScore / $totalHeaders) * 100
        Write-Log "Segurança HTTP: $securityScore/$totalHeaders ($($securityRate.ToString('F1'))%)" "INFO"
        
        return $securityRate -ge 60
    } catch {
        Write-Log "✗ Erro no teste de segurança HTTP: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar responsividade
function Test-Responsiveness {
    Write-Log "Testando responsividade..." "INFO"
    
    try {
        $response = Invoke-WebRequest -Uri $FrontendUrl -Method GET -UseBasicParsing -TimeoutSec 30
        $content = $response.Content
        
        $responsiveElements = @(
            "viewport",
            "media",
            "responsive",
            "bootstrap",
            "primeng",
            "flex"
        )
        
        $foundElements = 0
        $totalElements = $responsiveElements.Count
        
        foreach ($element in $responsiveElements) {
            if ($content -like "*$element*") {
                Write-Log "✓ Elemento responsivo encontrado: $element" "SUCCESS"
                $foundElements++
            } else {
                Write-Log "⚠ Elemento responsivo não encontrado: $element" "WARN"
            }
        }
        
        $responsiveRate = ($foundElements / $totalElements) * 100
        Write-Log "Responsividade: $foundElements/$totalElements ($($responsiveRate.ToString('F1'))%)" "INFO"
        
        return $responsiveRate -ge 50
    } catch {
        Write-Log "✗ Erro no teste de responsividade: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para verificar se o build está otimizado
function Test-BuildOptimization {
    Write-Log "Testando otimização do build..." "INFO"
    
    try {
        $response = Invoke-WebRequest -Uri $FrontendUrl -Method GET -UseBasicParsing -TimeoutSec 30
        $content = $response.Content
        
        $optimizationChecks = @{
            "Minificação" = ($content -notlike "*    *" -and $content -notlike "*`n`n*")
            "Compressão" = ($response.Headers.ContainsKey("Content-Encoding"))
            "Cache" = ($response.Headers.ContainsKey("Cache-Control") -or $response.Headers.ContainsKey("ETag"))
            "Bundles" = ($content -like "*bundle*" -or $content -like "*chunk*")
        }
        
        $optimizationScore = 0
        $totalChecks = $optimizationChecks.Count
        
        foreach ($check in $optimizationChecks.GetEnumerator()) {
            if ($check.Value) {
                Write-Log "✓ Otimização presente: $($check.Key)" "SUCCESS"
                $optimizationScore++
            } else {
                Write-Log "⚠ Otimização ausente: $($check.Key)" "WARN"
            }
        }
        
        $optimizationRate = ($optimizationScore / $totalChecks) * 100
        Write-Log "Otimização do build: $optimizationScore/$totalChecks ($($optimizationRate.ToString('F1'))%)" "INFO"
        
        return $optimizationRate -ge 60
    } catch {
        Write-Log "✗ Erro no teste de otimização: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função principal de validação
function Invoke-FrontendValidation {
    Write-Log "=== Iniciando Validação do Frontend ===" "INFO"
    Write-Log "Ambiente: $Environment" "INFO"
    Write-Log "URL do Frontend: $FrontendUrl" "INFO"
    
    $validationResults = @{}
    
    # 1. Disponibilidade
    $validationResults["Disponibilidade"] = Test-FrontendAvailability
    
    if (-not $validationResults["Disponibilidade"]) {
        Write-Log "Frontend não está disponível. Abortando testes." "ERROR"
        return $false
    }
    
    # 2. Carregamento de recursos
    $validationResults["Carregamento de Recursos"] = Test-ResourceLoading
    
    # 3. Conteúdo HTML
    $validationResults["Conteúdo HTML"] = Test-HTMLContent
    
    # 4. Performance
    $validationResults["Performance"] = Test-LoadingPerformance
    
    # 5. Conectividade com API
    $validationResults["Conectividade API"] = Test-APIConnectivity
    
    # 6. Segurança HTTP
    $validationResults["Segurança HTTP"] = Test-HTTPSecurity
    
    # 7. Responsividade
    $validationResults["Responsividade"] = Test-Responsiveness
    
    # 8. Otimização do build
    $validationResults["Otimização"] = Test-BuildOptimization
    
    # Resultado final
    $successCount = ($validationResults.Values | Where-Object { $_ -eq $true }).Count
    $totalCount = $validationResults.Count
    $successRate = ($successCount / $totalCount) * 100
    
    Write-Log "=== Resultado da Validação do Frontend ===" "INFO"
    Write-Log "Aprovados: $successCount/$totalCount ($($successRate.ToString('F1'))%)" "INFO"
    
    foreach ($result in $validationResults.GetEnumerator()) {
        $status = if ($result.Value) { "✓" } else { "✗" }
        $level = if ($result.Value) { "SUCCESS" } else { "ERROR" }
        Write-Log "$status $($result.Key)" $level
    }
    
    if ($successRate -ge 80) {
        Write-Log "✅ Frontend aprovado!" "SUCCESS"
        return $true
    } else {
        Write-Log "❌ Frontend reprovado!" "ERROR"
        return $false
    }
}

# Executar validação
$result = Invoke-FrontendValidation
exit $(if ($result) { 0 } else { 1 })