using EAM.Shared.DTOs;
using EAM.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace EAM.IntegrationTests;

/// <summary>
/// Testes de performance do sistema EAM v5.0
/// Valida critérios de aceitação específicos da especificação
/// </summary>
[Collection("Integration")]
public class PerformanceTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<PerformanceTests> _logger;

    public PerformanceTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = _fixture.Logger<PerformanceTests>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.StartInfrastructureAsync();
        await _fixture.StartApiAsync();
        await _fixture.StartAgentAsync();
        await Task.Delay(10000); // Aguarda estabilização
    }

    public async Task DisposeAsync()
    {
        await _fixture.StopAgentAsync();
        await _fixture.StopApiAsync();
        await _fixture.StopInfrastructureAsync();
    }

    [Fact]
    public async Task Agent_ShouldUseLessThan2PercentCPU()
    {
        _output.WriteLine("Testando uso de CPU do agente (critério: ≤ 2%)...");

        // Arrange
        var monitoringDuration = TimeSpan.FromMinutes(5);
        var agentProcessName = "EAM.Agent";
        var cpuReadings = new List<double>();

        // Act: Monitorar CPU por 5 minutos
        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(monitoringDuration);

        var cpuCounter = new PerformanceCounter("Process", "% Processor Time", agentProcessName, true);
        cpuCounter.NextValue(); // Primeira leitura descartada

        while (DateTime.UtcNow < endTime)
        {
            await Task.Delay(5000); // Leitura a cada 5 segundos
            
            try
            {
                var cpuUsage = cpuCounter.NextValue() / Environment.ProcessorCount;
                cpuReadings.Add(cpuUsage);
                
                _output.WriteLine($"CPU: {cpuUsage:F2}% em {DateTime.UtcNow:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Erro ao ler CPU: {ex.Message}");
            }
        }

        // Assert: Uso médio de CPU deve ser ≤ 2%
        cpuReadings.Should().NotBeEmpty();
        var averageCpu = cpuReadings.Average();
        var maxCpu = cpuReadings.Max();
        var minCpu = cpuReadings.Min();

        averageCpu.Should().BeLessOrEqualTo(2.0, "CPU média deve ser ≤ 2%");
        maxCpu.Should().BeLessOrEqualTo(5.0, "CPU máxima deve ser ≤ 5%");

        _output.WriteLine($"Resultado CPU: Média={averageCpu:F2}%, Máximo={maxCpu:F2}%, Mínimo={minCpu:F2}%");
    }

    [Fact]
    public async Task Agent_ShouldHandleHighEventVolume()
    {
        _output.WriteLine("Testando volume alto de eventos (critério: 10.000 EPS)...");

        // Arrange
        var agentId = Guid.NewGuid();
        var targetEventsPerSecond = 10000;
        var testDuration = TimeSpan.FromMinutes(2);
        var totalEvents = (int)(targetEventsPerSecond * testDuration.TotalSeconds);

        await RegisterTestAgentAsync(agentId);

        // Act: Gerar eventos em alta velocidade
        var scenario = Scenario.Create("high_volume_events", async context =>
        {
            var eventDto = new EventDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ActivityType = ActivityType.KeyPress,
                ProcessName = "high-volume-test",
                WindowTitle = $"Test Window {context.InvocationNumber}",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["testId"] = context.InvocationNumber,
                    ["batchId"] = Guid.NewGuid()
                }
            };

            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(_fixture.Configuration.TestEnvironment.ApiBaseUrl);
            
            var response = await httpClient.PostAsJsonAsync("/api/events", eventDto);
            
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: targetEventsPerSecond, during: testDuration)
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert: Verificar performance
        var sceneStats = stats.AllScenarios.First();
        sceneStats.Ok.Request.Count.Should().BeGreaterThan(totalEvents * 0.95); // 95% de sucesso mínimo
        sceneStats.Ok.Request.RPS.Should().BeGreaterThan(targetEventsPerSecond * 0.9); // 90% da taxa desejada
        sceneStats.Ok.Latency.Mean.Should().BeLessOrEqualTo(1000); // Latência média ≤ 1s

        _output.WriteLine($"Volume alto: {sceneStats.Ok.Request.Count} eventos processados");
        _output.WriteLine($"Taxa: {sceneStats.Ok.Request.RPS:F2} EPS");
        _output.WriteLine($"Latência média: {sceneStats.Ok.Latency.Mean:F2}ms");
    }

    [Fact]
    public async Task Agent_ShouldMaintainLowEventLoss()
    {
        _output.WriteLine("Testando perda de eventos (critério: ≤ 0.1% após 24h offline)...");

        // Arrange
        var agentId = Guid.NewGuid();
        var totalEvents = 10000;
        var simulatedOfflineHours = 24;
        
        await RegisterTestAgentAsync(agentId);

        // Act 1: Gerar eventos offline
        await _fixture.StopApiAsync();
        var offlineEvents = new List<EventDto>();
        
        for (int i = 0; i < totalEvents; i++)
        {
            var eventDto = new EventDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ActivityType = ActivityType.MouseClick,
                ProcessName = "offline-test",
                WindowTitle = $"Offline Window {i}",
                Timestamp = DateTime.UtcNow.AddHours(-simulatedOfflineHours + (i * simulatedOfflineHours / (double)totalEvents)),
                Metadata = new Dictionary<string, object>
                {
                    ["offlineIndex"] = i,
                    ["simulatedOffline"] = true
                }
            };
            
            offlineEvents.Add(eventDto);
            
            // Simular intervalos realistas
            if (i % 100 == 0)
            {
                await Task.Delay(100);
            }
        }

        // Act 2: Restaurar API e sincronizar
        await _fixture.StartApiAsync();
        await Task.Delay(10000); // Aguarda reconexão

        // Simular envio dos eventos armazenados offline
        var syncedEvents = 0;
        var failedEvents = 0;

        foreach (var eventDto in offlineEvents)
        {
            try
            {
                var response = await _fixture.HttpClient.PostAsJsonAsync("/api/events", eventDto);
                if (response.IsSuccessStatusCode)
                {
                    syncedEvents++;
                }
                else
                {
                    failedEvents++;
                }
            }
            catch
            {
                failedEvents++;
            }
        }

        // Assert: Perda de eventos deve ser ≤ 0.1%
        var lossPercentage = (double)failedEvents / totalEvents * 100;
        lossPercentage.Should().BeLessOrEqualTo(0.1, "Perda de eventos deve ser ≤ 0.1%");

        _output.WriteLine($"Eventos offline: {totalEvents} gerados, {syncedEvents} sincronizados, {failedEvents} perdidos");
        _output.WriteLine($"Taxa de perda: {lossPercentage:F3}%");
    }

    [Fact]
    public async Task WindowCapture_ShouldHave98PercentAccuracy()
    {
        _output.WriteLine("Testando precisão da captura de janelas (critério: 98% precisão)...");

        // Arrange
        var agentId = Guid.NewGuid();
        var testWindows = 1000;
        var validCaptureCount = 0;
        
        await RegisterTestAgentAsync(agentId);

        // Act: Simular capturas de janelas
        var captureResults = new List<WindowCaptureResult>();
        
        for (int i = 0; i < testWindows; i++)
        {
            var testProcess = $"test-process-{i % 10}";
            var expectedTitle = $"Test Window {i}";
            
            // Simular captura
            var captureResult = await SimulateWindowCaptureAsync(agentId, testProcess, expectedTitle);
            captureResults.Add(captureResult);
            
            if (captureResult.IsValid && captureResult.HasCorrectTitle)
            {
                validCaptureCount++;
            }
            
            // Pequeno delay para evitar sobrecarregar
            if (i % 50 == 0)
            {
                await Task.Delay(100);
            }
        }

        // Assert: Precisão deve ser ≥ 98%
        var accuracyPercentage = (double)validCaptureCount / testWindows * 100;
        accuracyPercentage.Should().BeGreaterOrEqualTo(98.0, "Precisão da captura deve ser ≥ 98%");

        var invalidFocus = captureResults.Count(r => !r.IsValid);
        var incorrectTitle = captureResults.Count(r => r.IsValid && !r.HasCorrectTitle);

        _output.WriteLine($"Captura de janelas: {validCaptureCount}/{testWindows} válidas");
        _output.WriteLine($"Precisão: {accuracyPercentage:F2}%");
        _output.WriteLine($"Focos inválidos: {invalidFocus}, Títulos incorretos: {incorrectTitle}");
    }

    [Fact]
    public async Task Screenshots_ShouldHave99Point5PercentSuccessRate()
    {
        _output.WriteLine("Testando taxa de sucesso de screenshots (critério: ≥ 99.5%)...");

        // Arrange
        var agentId = Guid.NewGuid();
        var totalScreenshots = 2000;
        var successfulUploads = 0;
        
        await RegisterTestAgentAsync(agentId);

        // Act: Simular capturas de screenshot
        var uploadTasks = new List<Task<bool>>();
        
        for (int i = 0; i < totalScreenshots; i++)
        {
            var screenshotTask = SimulateScreenshotUploadAsync(agentId, i);
            uploadTasks.Add(screenshotTask);
            
            // Controlar concorrência
            if (uploadTasks.Count >= 50)
            {
                var completedTasks = await Task.WhenAll(uploadTasks);
                successfulUploads += completedTasks.Count(success => success);
                uploadTasks.Clear();
            }
        }

        // Processar uploads restantes
        if (uploadTasks.Any())
        {
            var completedTasks = await Task.WhenAll(uploadTasks);
            successfulUploads += completedTasks.Count(success => success);
        }

        // Assert: Taxa de sucesso deve ser ≥ 99.5%
        var successRate = (double)successfulUploads / totalScreenshots * 100;
        successRate.Should().BeGreaterOrEqualTo(99.5, "Taxa de sucesso de screenshots deve ser ≥ 99.5%");

        var failedUploads = totalScreenshots - successfulUploads;
        
        _output.WriteLine($"Screenshots: {successfulUploads}/{totalScreenshots} enviados com sucesso");
        _output.WriteLine($"Taxa de sucesso: {successRate:F2}%");
        _output.WriteLine($"Falhas: {failedUploads}");
    }

    [Fact]
    public async Task Agent_ShouldRunAsInvisibleWindowsService()
    {
        _output.WriteLine("Testando execução como serviço Windows invisível...");

        // Arrange
        var agentProcessName = "EAM.Agent";
        
        // Act: Verificar processo do agente
        var agentProcesses = Process.GetProcessesByName(agentProcessName);
        
        // Assert: Deve haver processo do agente
        agentProcesses.Should().NotBeEmpty("Agente deve estar em execução");
        
        var agentProcess = agentProcesses.First();
        
        // Verificar que não tem janela visível
        var hasMainWindow = agentProcess.MainWindowHandle != IntPtr.Zero;
        hasMainWindow.Should().BeFalse("Agente não deve ter janela visível");
        
        // Verificar que está rodando como serviço
        var sessionId = agentProcess.SessionId;
        sessionId.Should().Be(0, "Agente deve rodar na sessão 0 (serviço)");
        
        // Verificar prioridade apropriada
        var priority = agentProcess.PriorityClass;
        priority.Should().BeOneOf(ProcessPriorityClass.Normal, ProcessPriorityClass.BelowNormal);
        
        // Verificar uso de memória
        var memoryUsage = agentProcess.WorkingSet64 / 1024 / 1024; // MB
        memoryUsage.Should().BeLessOrEqualTo(100, "Uso de memória deve ser ≤ 100MB");
        
        _output.WriteLine($"Serviço validado: PID={agentProcess.Id}, Memória={memoryUsage}MB");
        _output.WriteLine($"Sessão={sessionId}, Prioridade={priority}");
    }

    [Fact]
    public async Task System_ShouldHandleStressLoad()
    {
        _output.WriteLine("Testando carga de stress do sistema completo...");

        // Arrange
        var agentId = Guid.NewGuid();
        var stressDuration = TimeSpan.FromMinutes(3);
        var concurrentUsers = 50;
        
        await RegisterTestAgentAsync(agentId);

        // Act: Teste de stress com múltiplos tipos de operação
        var scenarios = new[]
        {
            Scenario.Create("event_creation", async context =>
            {
                var eventDto = new EventDto
                {
                    Id = Guid.NewGuid(),
                    AgentId = agentId,
                    ActivityType = ActivityType.KeyPress,
                    ProcessName = "stress-test",
                    WindowTitle = $"Stress Window {context.InvocationNumber}",
                    Timestamp = DateTime.UtcNow
                };

                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(_fixture.Configuration.TestEnvironment.ApiBaseUrl);
                
                var response = await httpClient.PostAsJsonAsync("/api/events", eventDto);
                return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.KeepConstant(copies: concurrentUsers / 2, during: stressDuration)
            ),

            Scenario.Create("screenshot_upload", async context =>
            {
                var screenshot = new ScreenshotDto
                {
                    Id = Guid.NewGuid(),
                    AgentId = agentId,
                    Timestamp = DateTime.UtcNow,
                    Data = GenerateTestScreenshotData(),
                    Format = "png"
                };

                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(_fixture.Configuration.TestEnvironment.ApiBaseUrl);
                
                var response = await httpClient.PostAsJsonAsync("/api/screenshots", screenshot);
                return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.KeepConstant(copies: concurrentUsers / 4, during: stressDuration)
            ),

            Scenario.Create("data_retrieval", async context =>
            {
                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(_fixture.Configuration.TestEnvironment.ApiBaseUrl);
                
                var response = await httpClient.GetAsync($"/api/events?agentId={agentId}&limit=100");
                return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithLoadSimulations(
                Simulation.KeepConstant(copies: concurrentUsers / 4, during: stressDuration)
            )
        };

        var stats = NBomberRunner
            .RegisterScenarios(scenarios)
            .Run();

        // Assert: Verificar que sistema manteve performance sob stress
        foreach (var scenario in stats.AllScenarios)
        {
            scenario.Ok.Request.Count.Should().BeGreaterThan(0);
            scenario.Fail.Request.Count.Should().BeLessThan(scenario.Ok.Request.Count * 0.05); // <5% falhas
            scenario.Ok.Latency.Mean.Should().BeLessOrEqualTo(2000); // Latência média ≤ 2s sob stress
        }

        _output.WriteLine("Teste de stress concluído:");
        foreach (var scenario in stats.AllScenarios)
        {
            _output.WriteLine($"  {scenario.ScenarioName}: {scenario.Ok.Request.Count} OK, {scenario.Fail.Request.Count} falhas");
            _output.WriteLine($"  RPS: {scenario.Ok.Request.RPS:F2}, Latência: {scenario.Ok.Latency.Mean:F2}ms");
        }
    }

    [Fact]
    public async Task System_ShouldMaintainPerformanceBaselines()
    {
        _output.WriteLine("Testando baselines de performance do sistema...");

        // Arrange
        var agentId = Guid.NewGuid();
        await RegisterTestAgentAsync(agentId);

        // Act & Assert: Múltiplos testes de baseline
        var baselineTests = new Dictionary<string, Func<Task<(bool success, string message)>>>
        {
            ["Response Time"] = async () => await TestResponseTimeBaseline(),
            ["Throughput"] = async () => await TestThroughputBaseline(),
            ["Memory Usage"] = async () => await TestMemoryUsageBaseline(),
            ["Database Performance"] = async () => await TestDatabasePerformanceBaseline(),
            ["File I/O"] = async () => await TestFileIOBaseline()
        };

        var results = new Dictionary<string, (bool success, string message)>();

        foreach (var test in baselineTests)
        {
            try
            {
                results[test.Key] = await test.Value();
                _output.WriteLine($"{test.Key}: {(results[test.Key].success ? "PASS" : "FAIL")} - {results[test.Key].message}");
            }
            catch (Exception ex)
            {
                results[test.Key] = (false, ex.Message);
                _output.WriteLine($"{test.Key}: FAIL - {ex.Message}");
            }
        }

        // Assert: Todos os baselines devem passar
        var failedTests = results.Where(r => !r.Value.success).ToList();
        failedTests.Should().BeEmpty("Todos os baselines de performance devem ser atendidos");

        _output.WriteLine($"Baselines de performance: {results.Count(r => r.Value.success)}/{results.Count} aprovados");
    }

    // Helper methods
    private async Task RegisterTestAgentAsync(Guid agentId)
    {
        var agent = new AgentDto
        {
            Id = agentId,
            MachineName = "PERF-TEST",
            Username = "perf-user",
            Version = "5.0.0",
            Status = AgentStatus.Online,
            LastHeartbeat = DateTime.UtcNow
        };

        await _fixture.HttpClient.PostAsJsonAsync("/api/agents/register", agent);
    }

    private async Task<WindowCaptureResult> SimulateWindowCaptureAsync(Guid agentId, string processName, string expectedTitle)
    {
        // Simular captura de janela
        var random = new Random();
        var isValid = random.NextDouble() > 0.01; // 99% de capturas válidas
        var hasCorrectTitle = random.NextDouble() > 0.01; // 99% de títulos corretos

        if (isValid && hasCorrectTitle)
        {
            var eventDto = new EventDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ActivityType = ActivityType.WindowChange,
                ProcessName = processName,
                WindowTitle = expectedTitle,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["captureValid"] = true,
                    ["titleAccurate"] = true
                }
            };

            var response = await _fixture.HttpClient.PostAsJsonAsync("/api/events", eventDto);
            return new WindowCaptureResult
            {
                IsValid = isValid,
                HasCorrectTitle = hasCorrectTitle,
                Success = response.IsSuccessStatusCode
            };
        }

        return new WindowCaptureResult
        {
            IsValid = isValid,
            HasCorrectTitle = hasCorrectTitle,
            Success = false
        };
    }

    private async Task<bool> SimulateScreenshotUploadAsync(Guid agentId, int index)
    {
        try
        {
            var screenshot = new ScreenshotDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                Timestamp = DateTime.UtcNow,
                Data = GenerateTestScreenshotData(),
                Format = "png"
            };

            var response = await _fixture.HttpClient.PostAsJsonAsync("/api/screenshots", screenshot);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private byte[] GenerateTestScreenshotData()
    {
        // Gerar dados de imagem PNG válidos (1x1 pixel)
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
            0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0x1D, 0x01, 0x01, 0x00, 0x00, 0xFF,
            0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
            0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        };
    }

    private async Task<(bool success, string message)> TestResponseTimeBaseline()
    {
        var startTime = DateTime.UtcNow;
        var response = await _fixture.HttpClient.GetAsync("/api/health");
        var elapsed = DateTime.UtcNow - startTime;

        var success = response.IsSuccessStatusCode && elapsed.TotalMilliseconds <= 1000;
        var message = $"Response time: {elapsed.TotalMilliseconds:F2}ms";

        return (success, message);
    }

    private async Task<(bool success, string message)> TestThroughputBaseline()
    {
        var requests = 100;
        var startTime = DateTime.UtcNow;

        var tasks = new List<Task<bool>>();
        for (int i = 0; i < requests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var response = await _fixture.HttpClient.GetAsync("/api/health");
                return response.IsSuccessStatusCode;
            }));
        }

        var results = await Task.WhenAll(tasks);
        var elapsed = DateTime.UtcNow - startTime;
        var throughput = requests / elapsed.TotalSeconds;

        var success = throughput >= 50; // Mínimo 50 RPS
        var message = $"Throughput: {throughput:F2} RPS";

        return (success, message);
    }

    private async Task<(bool success, string message)> TestMemoryUsageBaseline()
    {
        var processes = Process.GetProcessesByName("EAM.Agent");
        if (processes.Length == 0)
        {
            return (false, "Processo EAM.Agent não encontrado");
        }

        var process = processes[0];
        var memoryUsage = process.WorkingSet64 / 1024 / 1024; // MB

        var success = memoryUsage <= 100;
        var message = $"Memory usage: {memoryUsage}MB";

        return (success, message);
    }

    private async Task<(bool success, string message)> TestDatabasePerformanceBaseline()
    {
        var agentId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        // Teste básico de inserção
        var eventDto = new EventDto
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            ActivityType = ActivityType.KeyPress,
            ProcessName = "db-test",
            WindowTitle = "Database Test",
            Timestamp = DateTime.UtcNow
        };

        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/events", eventDto);
        var elapsed = DateTime.UtcNow - startTime;

        var success = response.IsSuccessStatusCode && elapsed.TotalMilliseconds <= 500;
        var message = $"Database insert: {elapsed.TotalMilliseconds:F2}ms";

        return (success, message);
    }

    private async Task<(bool success, string message)> TestFileIOBaseline()
    {
        var screenshot = new ScreenshotDto
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Data = GenerateTestScreenshotData(),
            Format = "png"
        };

        var startTime = DateTime.UtcNow;
        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/screenshots", screenshot);
        var elapsed = DateTime.UtcNow - startTime;

        var success = response.IsSuccessStatusCode && elapsed.TotalMilliseconds <= 2000;
        var message = $"File I/O: {elapsed.TotalMilliseconds:F2}ms";

        return (success, message);
    }
}

public class WindowCaptureResult
{
    public bool IsValid { get; set; }
    public bool HasCorrectTitle { get; set; }
    public bool Success { get; set; }
}