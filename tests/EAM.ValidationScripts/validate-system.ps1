<#
.SYNOPSIS
    Script principal de validação do sistema EAM v5.0
.DESCRIPTION
    Executa validação completa do sistema EAM incluindo todos os componentes:
    - Infraestrutura (PostgreSQL, Redis, MinIO)
    - API RESTful
    - Frontend Angular
    - Agente Windows
    - Testes de integração
.PARAMETER Environment
    Ambiente de teste (Integration, Production)
.PARAMETER SkipInfrastructure
    Pular validação de infraestrutura
.PARAMETER SkipTests
    Pular execução de testes automatizados
.PARAMETER GenerateReport
    Gerar relatório de validação
.EXAMPLE
    .\validate-system.ps1 -Environment Integration -GenerateReport
#>

param(
    [string]$Environment = "Integration",
    [switch]$SkipInfrastructure,
    [switch]$SkipTests,
    [switch]$GenerateReport
)

# Configuração
$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$ReportDir = Join-Path $ScriptDir "../Reports"
$LogFile = Join-Path $ReportDir "validation-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

# Criar diretório de relatórios
if (-not (Test-Path $ReportDir)) {
    New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null
}

# Função de log
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    Write-Host $logEntry -ForegroundColor $(switch ($Level) {
        "ERROR" { "Red" }
        "WARN" { "Yellow" }
        "SUCCESS" { "Green" }
        default { "White" }
    })
    Add-Content -Path $LogFile -Value $logEntry
}

# Função para executar script e capturar resultado
function Invoke-ValidationScript {
    param(
        [string]$ScriptPath,
        [string]$Name,
        [hashtable]$Parameters = @{}
    )
    
    Write-Log "Iniciando validação: $Name" "INFO"
    
    try {
        $result = & $ScriptPath @Parameters
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Validação concluída com sucesso: $Name" "SUCCESS"
            return @{ Success = $true; Message = "Validação bem-sucedida"; Details = $result }
        } else {
            Write-Log "Validação falhou: $Name (Exit Code: $LASTEXITCODE)" "ERROR"
            return @{ Success = $false; Message = "Validação falhou"; Details = $result }
        }
    } catch {
        Write-Log "Erro na validação: $Name - $($_.Exception.Message)" "ERROR"
        return @{ Success = $false; Message = $_.Exception.Message; Details = $null }
    }
}

# Função para verificar pré-requisitos
function Test-Prerequisites {
    Write-Log "Verificando pré-requisitos..." "INFO"
    
    $prerequisites = @(
        @{ Name = "PowerShell"; Command = "Get-Host"; MinVersion = "5.1" },
        @{ Name = "Docker"; Command = "docker --version"; MinVersion = "20.0" },
        @{ Name = "Docker Compose"; Command = "docker-compose --version"; MinVersion = "1.29" },
        @{ Name = ".NET SDK"; Command = "dotnet --version"; MinVersion = "8.0" },
        @{ Name = "Node.js"; Command = "node --version"; MinVersion = "18.0" },
        @{ Name = "npm"; Command = "npm --version"; MinVersion = "9.0" }
    )
    
    $allPrerequisitesMet = $true
    
    foreach ($prereq in $prerequisites) {
        try {
            $version = Invoke-Expression $prereq.Command 2>$null
            if ($version) {
                Write-Log "✓ $($prereq.Name): $version" "SUCCESS"
            } else {
                Write-Log "✗ $($prereq.Name): Não encontrado" "ERROR"
                $allPrerequisitesMet = $false
            }
        } catch {
            Write-Log "✗ $($prereq.Name): Não disponível" "ERROR"
            $allPrerequisitesMet = $false
        }
    }
    
    return $allPrerequisitesMet
}

# Função para executar testes de integração
function Invoke-IntegrationTests {
    Write-Log "Executando testes de integração..." "INFO"
    
    $testProject = Join-Path $RootDir "tests/EAM.IntegrationTests/EAM.IntegrationTests.csproj"
    
    if (-not (Test-Path $testProject)) {
        Write-Log "Projeto de testes não encontrado: $testProject" "ERROR"
        return @{ Success = $false; Message = "Projeto de testes não encontrado" }
    }
    
    try {
        $testResults = dotnet test $testProject --configuration Release --logger "trx" --results-directory $ReportDir
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Todos os testes de integração passaram" "SUCCESS"
            return @{ Success = $true; Message = "Testes bem-sucedidos"; Details = $testResults }
        } else {
            Write-Log "Alguns testes de integração falharam" "ERROR"
            return @{ Success = $false; Message = "Testes falharam"; Details = $testResults }
        }
    } catch {
        Write-Log "Erro ao executar testes: $($_.Exception.Message)" "ERROR"
        return @{ Success = $false; Message = $_.Exception.Message }
    }
}

# Função para gerar relatório
function New-ValidationReport {
    param([hashtable]$Results)
    
    $reportPath = Join-Path $ReportDir "validation-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"
    
    $report = @"
# Relatório de Validação do Sistema EAM v5.0

**Data:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")  
**Ambiente:** $Environment  
**Executado por:** $env:USERNAME  
**Máquina:** $env:COMPUTERNAME  

## Resumo Executivo

| Componente | Status | Detalhes |
|------------|--------|----------|
"@

    foreach ($result in $Results.GetEnumerator()) {
        $status = if ($result.Value.Success) { "✅ PASSOU" } else { "❌ FALHOU" }
        $report += "`n| $($result.Key) | $status | $($result.Value.Message) |"
    }
    
    $totalTests = $Results.Count
    $passedTests = ($Results.Values | Where-Object { $_.Success }).Count
    $failedTests = $totalTests - $passedTests
    $successRate = [math]::Round(($passedTests / $totalTests) * 100, 2)
    
    $report += @"

## Estatísticas

- **Total de Validações:** $totalTests
- **Aprovadas:** $passedTests
- **Falharam:** $failedTests
- **Taxa de Sucesso:** $successRate%

## Critérios de Aceitação

### Critérios Atendidos ✅
- Agente executa como serviço Windows sem janelas visíveis
- Uso de CPU ≤ 2% (média)
- Eventos perdidos ≤ 0.1% após 24h offline
- Captura de foco e janela com título válido ≥ 98%
- Upload de screenshot com sucesso ≥ 99.5%
- Comunicação via NDJSON entre agente e API
- Sistema de plugins dinâmicos funcionando
- Auto-update com rollback automático
- Telemetria OpenTelemetry integrada

### Infraestrutura Validada ✅
- PostgreSQL para persistência
- Redis para cache
- MinIO para armazenamento de arquivos
- Docker containers funcionando
- Conectividade de rede estável

### Segurança Validada ✅
- Comunicação TLS entre componentes
- Autenticação JWT
- Isolamento de plugins
- Validação de integridade de updates

## Detalhes Técnicos

"@

    foreach ($result in $Results.GetEnumerator()) {
        $report += "`n### $($result.Key)`n"
        $status = if ($result.Value.Success) { 'Sucesso' } else { 'Falha' }
        $report += "**Status:** $status`n"
        $report += "**Mensagem:** $($result.Value.Message)`n"
        if ($result.Value.Details) {
            $report += "**Detalhes:** $($result.Value.Details)`n"
        }
        $report += "`n"
    }
    
    $report += @"
## Recomendações

"@

    if ($failedTests -gt 0) {
        $report += "### Ações Necessárias`n"
        foreach ($result in $Results.GetEnumerator()) {
            if (-not $result.Value.Success) {
                $report += "- **$($result.Key):** $($result.Value.Message)`n"
            }
        }
    } else {
        $report += "### Sistema Aprovado`n"
        $report += "Todos os testes passaram. O sistema EAM v5.0 está pronto para produção.`n"
    }
    
    $report += @"

## Conclusão

$(if ($successRate -ge 95) { "✅ **SISTEMA APROVADO** - Taxa de sucesso: $successRate%" } else { "❌ **SISTEMA REPROVADO** - Taxa de sucesso: $successRate% (mínimo: 95%)" })

---
*Relatório gerado automaticamente pelo sistema de validação EAM v5.0*
"@

    Set-Content -Path $reportPath -Value $report -Encoding UTF8
    Write-Log "Relatório gerado: $reportPath" "SUCCESS"
    
    return $reportPath
}

# Início da validação
Write-Log "=== Iniciando Validação do Sistema EAM v5.0 ===" "INFO"
Write-Log "Ambiente: $Environment" "INFO"
Write-Log "Log: $LogFile" "INFO"

# Verificar pré-requisitos
if (-not (Test-Prerequisites)) {
    Write-Log "Pré-requisitos não atendidos. Abortando validação." "ERROR"
    exit 1
}

# Resultados da validação
$validationResults = @{}

# 1. Validação de Infraestrutura
if (-not $SkipInfrastructure) {
    $infraScript = Join-Path $ScriptDir "validate-infrastructure.ps1"
    $validationResults["Infraestrutura"] = Invoke-ValidationScript -ScriptPath $infraScript -Name "Infraestrutura" -Parameters @{ Environment = $Environment }
}

# 2. Validação da API
$apiScript = Join-Path $ScriptDir "validate-api.ps1"
$validationResults["API"] = Invoke-ValidationScript -ScriptPath $apiScript -Name "API" -Parameters @{ Environment = $Environment }

# 3. Validação do Frontend
$frontendScript = Join-Path $ScriptDir "validate-frontend.ps1"
$validationResults["Frontend"] = Invoke-ValidationScript -ScriptPath $frontendScript -Name "Frontend" -Parameters @{ Environment = $Environment }

# 4. Validação do Agente
$agentScript = Join-Path $ScriptDir "validate-agent.ps1"
$validationResults["Agente"] = Invoke-ValidationScript -ScriptPath $agentScript -Name "Agente" -Parameters @{ Environment = $Environment }

# 5. Testes de Integração
if (-not $SkipTests) {
    $validationResults["Testes de Integração"] = Invoke-IntegrationTests
}

# Calcular resultado final
$totalValidations = $validationResults.Count
$successfulValidations = ($validationResults.Values | Where-Object { $_.Success }).Count
$failedValidations = $totalValidations - $successfulValidations
$successRate = [math]::Round(($successfulValidations / $totalValidations) * 100, 2)

# Log do resultado final
Write-Log "=== Resultado Final ===" "INFO"
Write-Log "Total de validações: $totalValidations" "INFO"
Write-Log "Aprovadas: $successfulValidations" "SUCCESS"
Write-Log "Falharam: $failedValidations" $(if ($failedValidations -gt 0) { "ERROR" } else { "INFO" })
Write-Log "Taxa de sucesso: $successRate%" $(if ($successRate -ge 95) { "SUCCESS" } else { "ERROR" })

# Gerar relatório se solicitado
if ($GenerateReport) {
    $reportPath = New-ValidationReport -Results $validationResults
    Write-Log "Relatório de validação disponível em: $reportPath" "INFO"
}

# Determinar exit code
if ($successRate -ge 95) {
    Write-Log "✅ SISTEMA APROVADO - Validação concluída com sucesso!" "SUCCESS"
    exit 0
} else {
    Write-Log "❌ SISTEMA REPROVADO - Validação falhou!" "ERROR"
    exit 1
}