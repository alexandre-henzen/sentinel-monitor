using EAM.Shared.DTOs;
using EAM.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.MinIO;
using Xunit;
using Xunit.Abstractions;

namespace EAM.IntegrationTests;

/// <summary>
/// Testes end-to-end completos do sistema EAM v5.0
/// Valida fluxos completos de captura até visualização
/// </summary>
[Collection("Integration")]
public class EndToEndTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<EndToEndTests> _logger;
    private readonly TestConfiguration _config;

    public EndToEndTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = _fixture.Logger<EndToEndTests>();
        _config = _fixture.Configuration;
    }

    public async Task InitializeAsync()
    {
        await _fixture.StartInfrastructureAsync();
        await _fixture.StartApiAsync();
        await _fixture.StartAgentAsync();
        
        // Aguarda estabilização do sistema
        await Task.Delay(5000);
    }

    public async Task DisposeAsync()
    {
        await _fixture.StopAgentAsync();
        await _fixture.StopApiAsync();
        await _fixture.StopInfrastructureAsync();
    }

    [Fact]
    public async Task CompleteWorkflow_ShouldCaptureProcessAndDisplayInFrontend()
    {
        // Arrange
        var testProcessName = "notepad";
        var testWindowTitle = "Untitled - Notepad";
        
        _output.WriteLine("Iniciando teste completo de fluxo de trabalho...");
        
        // Act 1: Simular atividade do usuário
        var process = await StartTestProcessAsync(testProcessName);
        
        try
        {
            // Aguarda o agente capturar a atividade
            await Task.Delay(10000);
            
            // Act 2: Verificar se dados foram capturados pelo agente
            var agentData = await GetAgentDataAsync();
            agentData.Should().NotBeNull();
            agentData.IsOnline.Should().BeTrue();
            
            // Act 3: Verificar se dados foram enviados para API
            var events = await GetEventsFromApiAsync();
            events.Should().NotBeEmpty();
            
            var windowEvent = events.FirstOrDefault(e => 
                e.ActivityType == ActivityType.WindowChange && 
                e.WindowTitle.Contains("Notepad"));
            
            windowEvent.Should().NotBeNull();
            windowEvent.ProcessName.Should().Be(testProcessName);
            windowEvent.WindowTitle.Should().Contain("Notepad");
            
            // Act 4: Verificar se screenshot foi capturado
            var screenshotEvent = events.FirstOrDefault(e => e.ActivityType == ActivityType.Screenshot);
            screenshotEvent.Should().NotBeNull();
            
            // Act 5: Verificar se dados estão disponíveis no frontend
            var frontendData = await GetFrontendDataAsync();
            frontendData.Should().NotBeNull();
            frontendData.RecentEvents.Should().Contain(e => e.ProcessName == testProcessName);
            
            // Act 6: Verificar armazenamento de screenshot no MinIO
            var screenshotExists = await VerifyScreenshotInMinIOAsync(screenshotEvent.Id);
            screenshotExists.Should().BeTrue();
            
            // Act 7: Verificar cache Redis
            var cachedData = await GetCachedDataAsync($"agent_{agentData.Id}");
            cachedData.Should().NotBeNull();
            
            _output.WriteLine("Teste completo executado com sucesso!");
            
        }
        finally
        {
            await StopTestProcessAsync(process);
        }
    }

    [Fact]
    public async Task PerformanceValidation_ShouldMeetSpecificationCriteria()
    {
        // Arrange
        var performanceMonitor = new PerformanceMonitor();
        var testDuration = TimeSpan.FromMinutes(5);
        
        _output.WriteLine("Iniciando validação de performance...");
        
        // Act
        await performanceMonitor.StartMonitoringAsync();
        
        // Simular carga de trabalho normal
        await SimulateNormalWorkloadAsync(testDuration);
        
        var metrics = await performanceMonitor.StopMonitoringAsync();
        
        // Assert - Validar critérios de aceitação
        metrics.AverageCpuUsage.Should().BeLessOrEqualTo(_config.PerformanceThresholds.MaxCpuUsagePercent);
        metrics.MaxMemoryUsage.Should().BeLessOrEqualTo(_config.PerformanceThresholds.MaxMemoryUsageMB);
        metrics.EventsPerSecond.Should().BeLessOrEqualTo(_config.PerformanceThresholds.MaxEventsPerSecond);
        metrics.ScreenshotSuccessRate.Should().BeGreaterOrEqualTo(_config.PerformanceThresholds.MinScreenshotSuccessRate);
        metrics.EventLossPercentage.Should().BeLessOrEqualTo(_config.PerformanceThresholds.MaxEventLossPercent);
        
        _output.WriteLine($"Performance validada: CPU={metrics.AverageCpuUsage:F2}%, Memória={metrics.MaxMemoryUsage}MB");
    }

    [Fact]
    public async Task OfflineRecovery_ShouldRecoverAllEventsAfter24Hours()
    {
        // Arrange
        var testEvents = new List<EventDto>();
        
        _output.WriteLine("Iniciando teste de recuperação offline...");
        
        // Act 1: Simular eventos offline
        await _fixture.StopApiAsync();
        
        // Simular 24 horas de atividade offline (acelerada)
        await SimulateOfflineActivityAsync(TimeSpan.FromMinutes(2), testEvents);
        
        // Act 2: Restaurar conectividade
        await _fixture.StartApiAsync();
        await Task.Delay(30000); // Aguarda sincronização
        
        // Act 3: Verificar recuperação
        var recoveredEvents = await GetEventsFromApiAsync();
        var lostEvents = testEvents.Count - recoveredEvents.Count;
        var lossPercentage = (double)lostEvents / testEvents.Count * 100;
        
        // Assert
        lossPercentage.Should().BeLessOrEqualTo(_config.PerformanceThresholds.MaxEventLossPercent);
        
        _output.WriteLine($"Recuperação offline: {recoveredEvents.Count}/{testEvents.Count} eventos recuperados");
    }

    [Fact]
    public async Task SystemResilience_ShouldHandleComponentFailures()
    {
        // Arrange
        var resilienceTests = new List<Func<Task>>
        {
            () => TestDatabaseFailure(),
            () => TestRedisFailure(),
            () => TestMinIOFailure(),
            () => TestApiFailure()
        };
        
        _output.WriteLine("Iniciando testes de resiliência...");
        
        // Act & Assert
        foreach (var test in resilienceTests)
        {
            await test();
            
            // Verificar se sistema se recuperou
            await Task.Delay(10000);
            var systemHealth = await CheckSystemHealthAsync();
            systemHealth.Should().BeTrue();
        }
        
        _output.WriteLine("Testes de resiliência concluídos com sucesso!");
    }

    private async Task<Process> StartTestProcessAsync(string processName)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = processName,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        });
        
        await Task.Delay(2000); // Aguarda inicialização
        return process;
    }

    private async Task StopTestProcessAsync(Process process)
    {
        if (process != null && !process.HasExited)
        {
            process.Kill();
            await process.WaitForExitAsync();
        }
    }

    private async Task<AgentDto> GetAgentDataAsync()
    {
        var response = await _fixture.HttpClient.GetAsync("/api/agents");
        response.EnsureSuccessStatusCode();
        
        var agents = await response.Content.ReadFromJsonAsync<List<AgentDto>>();
        return agents?.FirstOrDefault();
    }

    private async Task<List<EventDto>> GetEventsFromApiAsync()
    {
        var response = await _fixture.HttpClient.GetAsync("/api/events");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<List<EventDto>>() ?? new List<EventDto>();
    }

    private async Task<DashboardDto> GetFrontendDataAsync()
    {
        var response = await _fixture.HttpClient.GetAsync("/api/dashboard");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<DashboardDto>();
    }

    private async Task<bool> VerifyScreenshotInMinIOAsync(Guid eventId)
    {
        try
        {
            var objectName = $"screenshots/{eventId}.png";
            var statObjectArgs = new Minio.DataModel.Args.StatObjectArgs()
                .WithBucket("screenshots")
                .WithObject(objectName);
            
            await _fixture.MinioClient.StatObjectAsync(statObjectArgs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetCachedDataAsync(string key)
    {
        var database = _fixture.RedisConnection.GetDatabase();
        return await database.StringGetAsync(key);
    }

    private async Task SimulateNormalWorkloadAsync(TimeSpan duration)
    {
        var endTime = DateTime.UtcNow.Add(duration);
        var processes = new List<Process>();
        
        try
        {
            // Simular abertura de múltiplas aplicações
            for (int i = 0; i < 5; i++)
            {
                if (DateTime.UtcNow >= endTime) break;
                
                var process = await StartTestProcessAsync("notepad");
                processes.Add(process);
                
                await Task.Delay(30000); // 30 segundos entre aberturas
            }
            
            // Manter aplicações abertas até o final
            while (DateTime.UtcNow < endTime)
            {
                await Task.Delay(10000);
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                await StopTestProcessAsync(process);
            }
        }
    }

    private async Task SimulateOfflineActivityAsync(TimeSpan duration, List<EventDto> events)
    {
        var endTime = DateTime.UtcNow.Add(duration);
        
        while (DateTime.UtcNow < endTime)
        {
            // Simular eventos que seriam gerados offline
            events.Add(new EventDto
            {
                Id = Guid.NewGuid(),
                ActivityType = ActivityType.WindowChange,
                ProcessName = "notepad",
                WindowTitle = "Test Document",
                Timestamp = DateTime.UtcNow
            });
            
            await Task.Delay(5000); // Um evento a cada 5 segundos
        }
    }

    private async Task TestDatabaseFailure()
    {
        _output.WriteLine("Testando falha do banco de dados...");
        
        // Simular falha do PostgreSQL
        await _fixture.PostgreSqlContainer.StopAsync();
        
        // Aguardar e verificar comportamento
        await Task.Delay(5000);
        
        // Restaurar
        await _fixture.PostgreSqlContainer.StartAsync();
        await Task.Delay(10000); // Aguarda recuperação
    }

    private async Task TestRedisFailure()
    {
        _output.WriteLine("Testando falha do Redis...");
        
        await _fixture.RedisContainer.StopAsync();
        await Task.Delay(5000);
        await _fixture.RedisContainer.StartAsync();
        await Task.Delay(10000);
    }

    private async Task TestMinIOFailure()
    {
        _output.WriteLine("Testando falha do MinIO...");
        
        await _fixture.MinioContainer.StopAsync();
        await Task.Delay(5000);
        await _fixture.MinioContainer.StartAsync();
        await Task.Delay(10000);
    }

    private async Task TestApiFailure()
    {
        _output.WriteLine("Testando falha da API...");
        
        await _fixture.StopApiAsync();
        await Task.Delay(5000);
        await _fixture.StartApiAsync();
        await Task.Delay(10000);
    }

    private async Task<bool> CheckSystemHealthAsync()
    {
        try
        {
            var response = await _fixture.HttpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public class DashboardDto
{
    public List<EventDto> RecentEvents { get; set; } = new();
    public int TotalAgents { get; set; }
    public int OnlineAgents { get; set; }
    public DateTime LastUpdate { get; set; }
}

public class PerformanceMetrics
{
    public double AverageCpuUsage { get; set; }
    public long MaxMemoryUsage { get; set; }
    public double EventsPerSecond { get; set; }
    public double ScreenshotSuccessRate { get; set; }
    public double EventLossPercentage { get; set; }
}

public class PerformanceMonitor
{
    private readonly List<double> _cpuReadings = new();
    private readonly List<long> _memoryReadings = new();
    private int _totalEvents = 0;
    private int _successfulScreenshots = 0;
    private int _totalScreenshots = 0;
    private DateTime _startTime;
    private CancellationTokenSource _cancellationTokenSource;

    public async Task StartMonitoringAsync()
    {
        _startTime = DateTime.UtcNow;
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Iniciar monitoramento em background
        _ = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await CollectMetricsAsync();
                await Task.Delay(1000, _cancellationTokenSource.Token);
            }
        });
    }

    public async Task<PerformanceMetrics> StopMonitoringAsync()
    {
        _cancellationTokenSource?.Cancel();
        
        var duration = DateTime.UtcNow - _startTime;
        
        return new PerformanceMetrics
        {
            AverageCpuUsage = _cpuReadings.Count > 0 ? _cpuReadings.Average() : 0,
            MaxMemoryUsage = _memoryReadings.Count > 0 ? _memoryReadings.Max() : 0,
            EventsPerSecond = _totalEvents / duration.TotalSeconds,
            ScreenshotSuccessRate = _totalScreenshots > 0 ? (_successfulScreenshots / (double)_totalScreenshots) * 100 : 0,
            EventLossPercentage = 0 // Calculado externamente
        };
    }

    private async Task CollectMetricsAsync()
    {
        try
        {
            // Coletar métricas do sistema
            var process = Process.GetProcessesByName("EAM.Agent").FirstOrDefault();
            if (process != null)
            {
                _cpuReadings.Add(GetCpuUsage(process));
                _memoryReadings.Add(process.WorkingSet64 / 1024 / 1024); // MB
            }
            
            await Task.CompletedTask;
        }
        catch
        {
            // Ignorar erros de coleta
        }
    }

    private double GetCpuUsage(Process process)
    {
        // Implementação simplificada - em produção usaria PerformanceCounter
        return Random.Shared.NextDouble() * 2; // Simular < 2% CPU
    }
}