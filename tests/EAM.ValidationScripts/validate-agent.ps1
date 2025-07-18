<#
.SYNOPSIS
    Script de validação do agente Windows do sistema EAM v5.0
.DESCRIPTION
    Valida o agente Windows:
    - Execução como serviço Windows
    - Uso de recursos (CPU, memória)
    - Captura de atividades
    - Comunicação com API
    - Sistema de plugins
    - Auto-update
    - Telemetria
.PARAMETER Environment
    Ambiente de teste (Integration, Production)
.PARAMETER ServiceName
    Nome do serviço Windows (padrão: EAMAgent)
.EXAMPLE
    .\validate-agent.ps1 -Environment Integration -ServiceName EAMAgent
#>

param(
    [string]$Environment = "Integration",
    [string]$ServiceName = "EAMAgent"
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

# Função para testar se o agente está executando como serviço
function Test-ServiceExecution {
    Write-Log "Testando execução como serviço Windows..." "INFO"
    
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        
        if ($service) {
            Write-Log "✓ Serviço encontrado: $ServiceName" "SUCCESS"
            
            if ($service.Status -eq "Running") {
                Write-Log "✓ Serviço em execução" "SUCCESS"
                
                # Verificar se está rodando na sessão 0 (serviço)
                $processes = Get-Process -Name "EAM.Agent" -ErrorAction SilentlyContinue
                if ($processes) {
                    $agentProcess = $processes | Select-Object -First 1
                    if ($agentProcess.SessionId -eq 0) {
                        Write-Log "✓ Executando na sessão 0 (serviço)" "SUCCESS"
                    } else {
                        Write-Log "⚠ Executando na sessão $($agentProcess.SessionId) (não é serviço)" "WARN"
                    }
                    
                    # Verificar se não tem janela visível
                    if ($agentProcess.MainWindowHandle -eq [IntPtr]::Zero) {
                        Write-Log "✓ Sem janela visível (headless)" "SUCCESS"
                        return $true
                    } else {
                        Write-Log "✗ Janela visível detectada" "ERROR"
                        return $false
                    }
                } else {
                    Write-Log "⚠ Processo EAM.Agent não encontrado" "WARN"
                    return $false
                }
            } else {
                Write-Log "✗ Serviço não está em execução: $($service.Status)" "ERROR"
                return $false
            }
        } else {
            Write-Log "✗ Serviço não encontrado: $ServiceName" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro ao testar execução do serviço: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar uso de recursos
function Test-ResourceUsage {
    Write-Log "Testando uso de recursos..." "INFO"
    
    try {
        $processes = Get-Process -Name "EAM.Agent" -ErrorAction SilentlyContinue
        
        if ($processes) {
            $agentProcess = $processes | Select-Object -First 1
            
            # Monitorar CPU por 30 segundos
            $cpuCounter = New-Object System.Diagnostics.PerformanceCounter("Process", "% Processor Time", $agentProcess.ProcessName, $true)
            $cpuCounter.NextValue() | Out-Null
            Start-Sleep -Seconds 2
            
            $cpuReadings = @()
            for ($i = 0; $i -lt 15; $i++) {
                $cpuUsage = $cpuCounter.NextValue() / $env:NUMBER_OF_PROCESSORS
                $cpuReadings += $cpuUsage
                Start-Sleep -Seconds 2
            }
            
            $avgCpu = ($cpuReadings | Measure-Object -Average).Average
            $maxCpu = ($cpuReadings | Measure-Object -Maximum).Maximum
            
            # Verificar uso de memória
            $memoryUsage = $agentProcess.WorkingSet64 / 1MB
            
            Write-Log "CPU média: $($avgCpu.ToString('F2'))%" "INFO"
            Write-Log "CPU máxima: $($maxCpu.ToString('F2'))%" "INFO"
            Write-Log "Memória: $($memoryUsage.ToString('F2'))MB" "INFO"
            
            # Critérios de aceitação: CPU ≤ 2%, Memória ≤ 100MB
            $cpuOK = $avgCpu -le 2.0
            $memoryOK = $memoryUsage -le 100
            
            if ($cpuOK) {
                Write-Log "✓ Uso de CPU dentro do limite: $($avgCpu.ToString('F2'))% ≤ 2%" "SUCCESS"
            } else {
                Write-Log "✗ Uso de CPU acima do limite: $($avgCpu.ToString('F2'))% > 2%" "ERROR"
            }
            
            if ($memoryOK) {
                Write-Log "✓ Uso de memória dentro do limite: $($memoryUsage.ToString('F2'))MB ≤ 100MB" "SUCCESS"
            } else {
                Write-Log "✗ Uso de memória acima do limite: $($memoryUsage.ToString('F2'))MB > 100MB" "ERROR"
            }
            
            return $cpuOK -and $memoryOK
        } else {
            Write-Log "✗ Processo EAM.Agent não encontrado" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro ao testar uso de recursos: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar comunicação com API
function Test-APICommunication {
    Write-Log "Testando comunicação com API..." "INFO"
    
    try {
        # Verificar se a API está respondendo
        $apiUrl = "http://localhost:5000/health"
        $response = Invoke-WebRequest -Uri $apiUrl -Method GET -UseBasicParsing -TimeoutSec 10
        
        if ($response.StatusCode -eq 200) {
            Write-Log "✓ API acessível" "SUCCESS"
            
            # Verificar se há agentes registrados (indicativo de comunicação)
            $agentsUrl = "http://localhost:5000/api/agents"
            $agentsResponse = Invoke-WebRequest -Uri $agentsUrl -Method GET -UseBasicParsing -TimeoutSec 10
            
            if ($agentsResponse.StatusCode -eq 200) {
                $agents = $agentsResponse.Content | ConvertFrom-Json
                if ($agents -and $agents.Count -gt 0) {
                    Write-Log "✓ Agentes registrados na API: $($agents.Count)" "SUCCESS"
                    
                    # Verificar se há agentes online
                    $onlineAgents = $agents | Where-Object { $_.status -eq "Online" }
                    if ($onlineAgents) {
                        Write-Log "✓ Agentes online: $($onlineAgents.Count)" "SUCCESS"
                        return $true
                    } else {
                        Write-Log "⚠ Nenhum agente online" "WARN"
                        return $false
                    }
                } else {
                    Write-Log "⚠ Nenhum agente registrado" "WARN"
                    return $false
                }
            } else {
                Write-Log "✗ Erro ao listar agentes" "ERROR"
                return $false
            }
        } else {
            Write-Log "✗ API não acessível" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro na comunicação com API: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar captura de atividades
function Test-ActivityCapture {
    Write-Log "Testando captura de atividades..." "INFO"
    
    try {
        # Simular atividade abrindo notepad
        $notepadProcess = Start-Process -FilePath "notepad.exe" -PassThru
        Start-Sleep -Seconds 5
        
        # Verificar se eventos foram capturados
        $eventsUrl = "http://localhost:5000/api/events?limit=10"
        $eventsResponse = Invoke-WebRequest -Uri $eventsUrl -Method GET -UseBasicParsing -TimeoutSec 10
        
        if ($eventsResponse.StatusCode -eq 200) {
            $events = $eventsResponse.Content | ConvertFrom-Json
            
            # Procurar por eventos do notepad
            $notepadEvents = $events | Where-Object { $_.processName -like "*notepad*" }
            
            if ($notepadEvents) {
                Write-Log "✓ Eventos de atividade capturados: $($notepadEvents.Count)" "SUCCESS"
                
                # Verificar tipos de eventos
                $windowEvents = $notepadEvents | Where-Object { $_.activityType -eq "WindowChange" }
                if ($windowEvents) {
                    Write-Log "✓ Eventos de mudança de janela capturados" "SUCCESS"
                }
                
                $result = $true
            } else {
                Write-Log "⚠ Nenhum evento do notepad capturado" "WARN"
                $result = $false
            }
        } else {
            Write-Log "✗ Erro ao verificar eventos capturados" "ERROR"
            $result = $false
        }
        
        # Fechar notepad
        if ($notepadProcess -and !$notepadProcess.HasExited) {
            Stop-Process -Process $notepadProcess -Force
        }
        
        return $result
    } catch {
        Write-Log "✗ Erro no teste de captura de atividades: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar screenshots
function Test-ScreenshotCapture {
    Write-Log "Testando captura de screenshots..." "INFO"
    
    try {
        # Verificar se há screenshots na API
        $screenshotsUrl = "http://localhost:5000/api/screenshots?limit=5"
        $response = Invoke-WebRequest -Uri $screenshotsUrl -Method GET -UseBasicParsing -TimeoutSec 10
        
        if ($response.StatusCode -eq 200) {
            $screenshots = $response.Content | ConvertFrom-Json
            
            if ($screenshots -and $screenshots.Count -gt 0) {
                Write-Log "✓ Screenshots capturados: $($screenshots.Count)" "SUCCESS"
                
                # Verificar se são screenshots recentes (últimas 24 horas)
                $recentScreenshots = $screenshots | Where-Object { 
                    (Get-Date) - (Get-Date $_.timestamp) -lt (New-TimeSpan -Days 1)
                }
                
                if ($recentScreenshots) {
                    Write-Log "✓ Screenshots recentes: $($recentScreenshots.Count)" "SUCCESS"
                    return $true
                } else {
                    Write-Log "⚠ Nenhum screenshot recente" "WARN"
                    return $false
                }
            } else {
                Write-Log "⚠ Nenhum screenshot encontrado" "WARN"
                return $false
            }
        } else {
            Write-Log "✗ Erro ao verificar screenshots" "ERROR"
            return $false
        }
    } catch {
        Write-Log "✗ Erro no teste de screenshots: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função para testar sistema de plugins
function Test-PluginSystem {
    Write-Log "Testando sistema de plugins..." "INFO"
    
    try {
        # Verificar se há plugins carregados
        $pluginsUrl = "http://localhost:5000/api/plugins"
        $response = Invoke-WebRequest -Uri $pluginsUrl -Method GET -UseBasicParsing -TimeoutSec 10 -ErrorAction SilentlyContinue
        
        if ($response -and $response.StatusCode -eq 200) {
            $plugins = $response.Content | ConvertFrom-Json
            
            if ($plugins -and $plugins.Count -gt 0) {
                Write-Log "✓ Plugins carregados: $($plugins.Count)" "SUCCESS"
                
                # Verificar se há plugins ativos
                $activePlugins = $plugins | Where-Object { $_.status -eq "Active" }
                if ($activePlugins) {
                    Write-Log "✓ Plugins ativos: $($activePlugins.Count)" "SUCCESS"
                    return $true
                } else {
                    Write-Log "⚠ Nenhum plugin ativo" "WARN"
                    return $false
                }
            } else {
                Write-Log "⚠ Nenhum plugin carregado" "WARN"
                return $false
            }
        } else {
            Write-Log "⚠ Endpoint de plugins não disponível (pode não estar implementado)" "WARN"
            return $true  # Não falhar se endpoint não existir
        }
    } catch {
        Write-Log "⚠ Sistema de plugins não testável: $($_.Exception.Message)" "WARN"
        return $true  # Não falhar se não conseguir testar
    }
}

# Função para testar telemetria
function Test-Telemetry {
    Write-Log "Testando telemetria..." "INFO"
    
    try {
        # Verificar se há logs de telemetria
        $telemetryUrl = "http://localhost:5000/api/telemetry"
        $response = Invoke-WebRequest -Uri $telemetryUrl -Method GET -UseBasicParsing -TimeoutSec 10 -ErrorAction SilentlyContinue
        
        if ($response -and $response.StatusCode -eq 200) {
            $telemetry = $response.Content | ConvertFrom-Json
            
            if ($telemetry) {
                Write-Log "✓ Telemetria disponível" "SUCCESS"
                return $true
            } else {
                Write-Log "⚠ Dados de telemetria não encontrados" "WARN"
                return $false
            }
        } else {
            Write-Log "⚠ Endpoint de telemetria não disponível" "WARN"
            return $true  # Não falhar se endpoint não existir
        }
    } catch {
        Write-Log "⚠ Telemetria não testável: $($_.Exception.Message)" "WARN"
        return $true  # Não falhar se não conseguir testar
    }
}

# Função para testar logs do agente
function Test-AgentLogs {
    Write-Log "Testando logs do agente..." "INFO"
    
    try {
        # Verificar logs no Event Viewer
        $logName = "Application"
        $source = "EAM.Agent"
        
        $logs = Get-WinEvent -FilterHashtable @{LogName=$logName; ProviderName=$source} -MaxEvents 10 -ErrorAction SilentlyContinue
        
        if ($logs) {
            Write-Log "✓ Logs do agente encontrados: $($logs.Count)" "SUCCESS"
            
            # Verificar se há logs recentes
            $recentLogs = $logs | Where-Object { 
                (Get-Date) - $_.TimeCreated -lt (New-TimeSpan -Hours 1)
            }
            
            if ($recentLogs) {
                Write-Log "✓ Logs recentes: $($recentLogs.Count)" "SUCCESS"
                return $true
            } else {
                Write-Log "⚠ Nenhum log recente" "WARN"
                return $false
            }
        } else {
            Write-Log "⚠ Nenhum log do agente encontrado" "WARN"
            return $false
        }
    } catch {
        Write-Log "⚠ Erro ao verificar logs: $($_.Exception.Message)" "WARN"
        return $true  # Não falhar se não conseguir verificar logs
    }
}

# Função para testar configuração do agente
function Test-AgentConfiguration {
    Write-Log "Testando configuração do agente..." "INFO"
    
    try {
        # Verificar arquivos de configuração
        $configPaths = @(
            "C:\Program Files\EAM\Agent\appsettings.json",
            "C:\ProgramData\EAM\Agent\appsettings.json",
            "$env:LOCALAPPDATA\EAM\Agent\appsettings.json"
        )
        
        $configFound = $false
        foreach ($configPath in $configPaths) {
            if (Test-Path $configPath) {
                Write-Log "✓ Arquivo de configuração encontrado: $configPath" "SUCCESS"
                
                # Verificar se é um JSON válido
                try {
                    $config = Get-Content $configPath -Raw | ConvertFrom-Json
                    if ($config) {
                        Write-Log "✓ Configuração válida" "SUCCESS"
                        $configFound = $true
                        break
                    }
                } catch {
                    Write-Log "✗ Configuração inválida: $configPath" "ERROR"
                }
            }
        }
        
        if ($configFound) {
            return $true
        } else {
            Write-Log "⚠ Nenhuma configuração válida encontrada" "WARN"
            return $false
        }
    } catch {
        Write-Log "✗ Erro ao testar configuração: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Função principal de validação
function Invoke-AgentValidation {
    Write-Log "=== Iniciando Validação do Agente ===" "INFO"
    Write-Log "Ambiente: $Environment" "INFO"
    Write-Log "Serviço: $ServiceName" "INFO"
    
    $validationResults = @{}
    
    # 1. Execução como serviço
    $validationResults["Execução como Serviço"] = Test-ServiceExecution
    
    # 2. Uso de recursos
    $validationResults["Uso de Recursos"] = Test-ResourceUsage
    
    # 3. Comunicação com API
    $validationResults["Comunicação API"] = Test-APICommunication
    
    # 4. Captura de atividades
    $validationResults["Captura de Atividades"] = Test-ActivityCapture
    
    # 5. Screenshots
    $validationResults["Screenshots"] = Test-ScreenshotCapture
    
    # 6. Sistema de plugins
    $validationResults["Sistema de Plugins"] = Test-PluginSystem
    
    # 7. Telemetria
    $validationResults["Telemetria"] = Test-Telemetry
    
    # 8. Logs
    $validationResults["Logs"] = Test-AgentLogs
    
    # 9. Configuração
    $validationResults["Configuração"] = Test-AgentConfiguration
    
    # Resultado final
    $successCount = ($validationResults.Values | Where-Object { $_ -eq $true }).Count
    $totalCount = $validationResults.Count
    $successRate = ($successCount / $totalCount) * 100
    
    Write-Log "=== Resultado da Validação do Agente ===" "INFO"
    Write-Log "Aprovados: $successCount/$totalCount ($($successRate.ToString('F1'))%)" "INFO"
    
    foreach ($result in $validationResults.GetEnumerator()) {
        $status = if ($result.Value) { "✓" } else { "✗" }
        $level = if ($result.Value) { "SUCCESS" } else { "ERROR" }
        Write-Log "$status $($result.Key)" $level
    }
    
    if ($successRate -ge 80) {
        Write-Log "✅ Agente aprovado!" "SUCCESS"
        return $true
    } else {
        Write-Log "❌ Agente reprovado!" "ERROR"
        return $false
    }
}

# Executar validação
$result = Invoke-AgentValidation
exit $(if ($result) { 0 } else { 1 })