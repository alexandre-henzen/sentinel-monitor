using EAM.Shared.DTOs;
using EAM.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace EAM.IntegrationTests;

/// <summary>
/// Testes de fluxo de dados completo no sistema EAM
/// Valida desde captura até visualização nos dashboards
/// </summary>
[Collection("Integration")]
public class DataFlowTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<DataFlowTests> _logger;

    public DataFlowTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = _fixture.Logger<DataFlowTests>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.StartInfrastructureAsync();
        await _fixture.StartApiAsync();
        await Task.Delay(5000);
    }

    public async Task DisposeAsync()
    {
        await _fixture.StopApiAsync();
        await _fixture.StopInfrastructureAsync();
    }

    [Fact]
    public async Task WindowTracker_ShouldCaptureAndFlowToFrontend()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var testScenario = new WindowTrackingScenario
        {
            AgentId = agentId,
            ProcessName = "notepad",
            WindowTitle = "Document.txt - Notepad",
            ExpectedActivityType = ActivityType.WindowChange
        };

        _output.WriteLine($"Testando fluxo de dados do WindowTracker para agente {agentId}");

        // Act 1: Simular captura pelo WindowTracker
        var capturedEvent = await SimulateWindowTrackerCaptureAsync(testScenario);
        
        // Act 2: Enviar para API
        var apiResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/events", capturedEvent);
        apiResponse.EnsureSuccessStatusCode();

        // Act 3: Aguardar processamento
        await Task.Delay(3000);

        // Act 4: Verificar persistência no banco
        var databaseEvents = await GetEventsFromDatabaseAsync(agentId);
        var persistedEvent = databaseEvents.FirstOrDefault(e => e.Id == capturedEvent.Id);
        
        // Assert: Evento persistido corretamente
        persistedEvent.Should().NotBeNull();
        persistedEvent.ProcessName.Should().Be(testScenario.ProcessName);
        persistedEvent.WindowTitle.Should().Be(testScenario.WindowTitle);
        persistedEvent.ActivityType.Should().Be(testScenario.ExpectedActivityType);

        // Act 5: Verificar disponibilidade no frontend
        var frontendResponse = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}");
        frontendResponse.EnsureSuccessStatusCode();
        
        var frontendEvents = await frontendResponse.Content.ReadFromJsonAsync<List<EventDto>>();
        var frontendEvent = frontendEvents?.FirstOrDefault(e => e.Id == capturedEvent.Id);
        
        // Assert: Evento disponível no frontend
        frontendEvent.Should().NotBeNull();
        frontendEvent.ProcessName.Should().Be(testScenario.ProcessName);
        
        _output.WriteLine($"Fluxo WindowTracker concluído com sucesso para evento {capturedEvent.Id}");
    }

    [Fact]
    public async Task BrowserTracker_ShouldCaptureWebActivity()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var browserScenario = new BrowserTrackingScenario
        {
            AgentId = agentId,
            ProcessName = "chrome",
            WindowTitle = "Google Chrome",
            Url = "https://www.example.com",
            BrowserType = "Chrome"
        };

        _output.WriteLine($"Testando fluxo de dados do BrowserTracker para agente {agentId}");

        // Act 1: Simular captura pelo BrowserTracker
        var capturedEvent = await SimulateBrowserTrackerCaptureAsync(browserScenario);
        
        // Act 2: Enviar para API
        var apiResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/events", capturedEvent);
        apiResponse.EnsureSuccessStatusCode();

        await Task.Delay(3000);

        // Act 3: Verificar dados específicos do browser
        var browserEvents = await GetBrowserEventsAsync(agentId);
        var browserEvent = browserEvents.FirstOrDefault(e => e.Id == capturedEvent.Id);
        
        // Assert: Dados específicos do browser preservados
        browserEvent.Should().NotBeNull();
        browserEvent.ProcessName.Should().Be(browserScenario.ProcessName);
        browserEvent.WindowTitle.Should().Contain("Chrome");
        browserEvent.Metadata.Should().ContainKey("url");
        browserEvent.Metadata.Should().ContainKey("browserType");
        
        _output.WriteLine($"Fluxo BrowserTracker concluído com sucesso para evento {capturedEvent.Id}");
    }

    [Fact]
    public async Task TeamsTracker_ShouldCaptureTeamsActivity()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var teamsScenario = new TeamsTrackingScenario
        {
            AgentId = agentId,
            ProcessName = "Teams",
            WindowTitle = "Microsoft Teams",
            MeetingId = "meeting-123",
            MeetingTitle = "Daily Standup",
            ParticipantCount = 5
        };

        _output.WriteLine($"Testando fluxo de dados do TeamsTracker para agente {agentId}");

        // Act 1: Simular captura pelo TeamsTracker
        var capturedEvent = await SimulateTeamsTrackerCaptureAsync(teamsScenario);
        
        // Act 2: Enviar para API
        var apiResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/events", capturedEvent);
        apiResponse.EnsureSuccessStatusCode();

        await Task.Delay(3000);

        // Act 3: Verificar dados específicos do Teams
        var teamsEvents = await GetTeamsEventsAsync(agentId);
        var teamsEvent = teamsEvents.FirstOrDefault(e => e.Id == capturedEvent.Id);
        
        // Assert: Dados específicos do Teams preservados
        teamsEvent.Should().NotBeNull();
        teamsEvent.ProcessName.Should().Be(teamsScenario.ProcessName);
        teamsEvent.Metadata.Should().ContainKey("meetingId");
        teamsEvent.Metadata.Should().ContainKey("meetingTitle");
        teamsEvent.Metadata.Should().ContainKey("participantCount");
        
        _output.WriteLine($"Fluxo TeamsTracker concluído com sucesso para evento {capturedEvent.Id}");
    }

    [Fact]
    public async Task ScreenshotCapture_ShouldFlowToMinIO()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var screenshotScenario = new ScreenshotCaptureScenario
        {
            AgentId = agentId,
            Width = 1920,
            Height = 1080,
            Quality = 85,
            Format = "png"
        };

        _output.WriteLine($"Testando fluxo de captura de screenshot para agente {agentId}");

        // Act 1: Simular captura de screenshot
        var capturedScreenshot = await SimulateScreenshotCaptureAsync(screenshotScenario);
        
        // Act 2: Enviar para API
        var apiResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/screenshots", capturedScreenshot);
        apiResponse.EnsureSuccessStatusCode();

        await Task.Delay(5000); // Screenshots levam mais tempo para processar

        // Act 3: Verificar armazenamento no MinIO
        var minioExists = await VerifyScreenshotInMinIOAsync(capturedScreenshot.Id);
        minioExists.Should().BeTrue();

        // Act 4: Verificar metadados no banco
        var screenshotMetadata = await GetScreenshotMetadataAsync(capturedScreenshot.Id);
        screenshotMetadata.Should().NotBeNull();
        screenshotMetadata.Width.Should().Be(screenshotScenario.Width);
        screenshotMetadata.Height.Should().Be(screenshotScenario.Height);
        screenshotMetadata.Format.Should().Be(screenshotScenario.Format);

        // Act 5: Verificar disponibilidade no frontend
        var frontendResponse = await _fixture.HttpClient.GetAsync($"/api/screenshots?agentId={agentId}");
        frontendResponse.EnsureSuccessStatusCode();
        
        var frontendScreenshots = await frontendResponse.Content.ReadFromJsonAsync<List<ScreenshotDto>>();
        var frontendScreenshot = frontendScreenshots?.FirstOrDefault(s => s.Id == capturedScreenshot.Id);
        
        frontendScreenshot.Should().NotBeNull();
        
        _output.WriteLine($"Fluxo Screenshot concluído com sucesso para screenshot {capturedScreenshot.Id}");
    }

    [Fact]
    public async Task BatchProcessing_ShouldHandleHighVolume()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var batchSize = 1000;
        var events = new List<EventDto>();

        for (int i = 0; i < batchSize; i++)
        {
            events.Add(new EventDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ActivityType = ActivityType.KeyPress,
                ProcessName = $"batch-process-{i % 10}",
                WindowTitle = $"Batch Window {i}",
                Timestamp = DateTime.UtcNow.AddSeconds(i),
                Metadata = new Dictionary<string, object>
                {
                    ["batchIndex"] = i,
                    ["batchId"] = Guid.NewGuid()
                }
            });
        }

        _output.WriteLine($"Testando processamento em lote de {batchSize} eventos");

        // Act 1: Enviar em lote via NDJSON
        var ndjsonContent = string.Join("\n", events.Select(e => JsonSerializer.Serialize(e)));
        var content = new StringContent(ndjsonContent, System.Text.Encoding.UTF8, "application/x-ndjson");
        
        var startTime = DateTime.UtcNow;
        var response = await _fixture.HttpClient.PostAsync("/api/events/batch", content);
        var processingTime = DateTime.UtcNow - startTime;
        
        // Assert: Batch processado com sucesso
        response.EnsureSuccessStatusCode();
        
        // Act 2: Aguardar processamento completo
        await Task.Delay(10000);
        
        // Act 3: Verificar todos os eventos foram persistidos
        var persistedEvents = await GetEventsFromDatabaseAsync(agentId);
        
        // Assert: Todos os eventos foram processados
        persistedEvents.Should().HaveCount(batchSize);
        
        // Assert: Performance dentro dos limites
        var eventsPerSecond = batchSize / processingTime.TotalSeconds;
        eventsPerSecond.Should().BeGreaterThan(100); // Mínimo 100 eventos/segundo
        
        _output.WriteLine($"Processamento em lote concluído: {eventsPerSecond:F2} eventos/segundo");
    }

    [Fact]
    public async Task RealTimeUpdates_ShouldReflectInFrontend()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var realTimeScenario = new RealTimeUpdateScenario
        {
            AgentId = agentId,
            UpdateInterval = TimeSpan.FromSeconds(5),
            TotalUpdates = 10
        };

        _output.WriteLine($"Testando atualizações em tempo real para agente {agentId}");

        // Act 1: Simular atualizações em tempo real
        var updateTasks = new List<Task>();
        var sentEvents = new List<EventDto>();

        for (int i = 0; i < realTimeScenario.TotalUpdates; i++)
        {
            var eventToSend = new EventDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ActivityType = ActivityType.MouseClick,
                ProcessName = $"realtime-app-{i}",
                WindowTitle = $"Real Time Window {i}",
                Timestamp = DateTime.UtcNow
            };

            sentEvents.Add(eventToSend);
            
            updateTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(i * 1000); // Spread updates over time
                await _fixture.HttpClient.PostAsJsonAsync("/api/events", eventToSend);
            }));
        }

        await Task.WhenAll(updateTasks);
        await Task.Delay(5000); // Aguarda processamento

        // Act 2: Verificar disponibilidade imediata no frontend
        var frontendEvents = await GetEventsFromFrontendAsync(agentId);
        
        // Assert: Todos os eventos em tempo real foram capturados
        frontendEvents.Should().HaveCount(realTimeScenario.TotalUpdates);
        
        foreach (var sentEvent in sentEvents)
        {
            var frontendEvent = frontendEvents.FirstOrDefault(e => e.Id == sentEvent.Id);
            frontendEvent.Should().NotBeNull();
            frontendEvent.ProcessName.Should().Be(sentEvent.ProcessName);
        }

        _output.WriteLine($"Atualizações em tempo real concluídas com sucesso: {sentEvents.Count} eventos");
    }

    [Fact]
    public async Task DataIntegrity_ShouldMaintainConsistency()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var testEvent = new EventDto
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            ActivityType = ActivityType.WindowChange,
            ProcessName = "integrity-test",
            WindowTitle = "Data Integrity Test",
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["testValue"] = "integrity-check",
                ["numericValue"] = 42,
                ["booleanValue"] = true
            }
        };

        _output.WriteLine($"Testando integridade de dados para evento {testEvent.Id}");

        // Act 1: Enviar evento
        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/events", testEvent);
        response.EnsureSuccessStatusCode();

        await Task.Delay(3000);

        // Act 2: Recuperar de múltiplas fontes
        var databaseEvent = (await GetEventsFromDatabaseAsync(agentId)).FirstOrDefault(e => e.Id == testEvent.Id);
        var frontendEvent = (await GetEventsFromFrontendAsync(agentId)).FirstOrDefault(e => e.Id == testEvent.Id);
        var cachedEvent = await GetCachedEventAsync(testEvent.Id);

        // Assert: Integridade mantida em todas as fontes
        databaseEvent.Should().NotBeNull();
        frontendEvent.Should().NotBeNull();
        cachedEvent.Should().NotBeNull();

        // Verificar campos básicos
        var events = new[] { databaseEvent, frontendEvent, cachedEvent };
        foreach (var evt in events)
        {
            evt.Id.Should().Be(testEvent.Id);
            evt.AgentId.Should().Be(testEvent.AgentId);
            evt.ProcessName.Should().Be(testEvent.ProcessName);
            evt.WindowTitle.Should().Be(testEvent.WindowTitle);
            evt.ActivityType.Should().Be(testEvent.ActivityType);
        }

        // Verificar metadados
        foreach (var evt in events)
        {
            evt.Metadata.Should().ContainKey("testValue");
            evt.Metadata.Should().ContainKey("numericValue");
            evt.Metadata.Should().ContainKey("booleanValue");
            evt.Metadata["testValue"].ToString().Should().Be("integrity-check");
        }

        _output.WriteLine($"Integridade de dados verificada com sucesso para evento {testEvent.Id}");
    }

    private async Task<EventDto> SimulateWindowTrackerCaptureAsync(WindowTrackingScenario scenario)
    {
        return new EventDto
        {
            Id = Guid.NewGuid(),
            AgentId = scenario.AgentId,
            ActivityType = scenario.ExpectedActivityType,
            ProcessName = scenario.ProcessName,
            WindowTitle = scenario.WindowTitle,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["captureSource"] = "WindowTracker",
                ["processId"] = Random.Shared.Next(1000, 9999),
                ["windowHandle"] = Random.Shared.Next(100000, 999999)
            }
        };
    }

    private async Task<EventDto> SimulateBrowserTrackerCaptureAsync(BrowserTrackingScenario scenario)
    {
        return new EventDto
        {
            Id = Guid.NewGuid(),
            AgentId = scenario.AgentId,
            ActivityType = ActivityType.BrowserNavigation,
            ProcessName = scenario.ProcessName,
            WindowTitle = scenario.WindowTitle,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["captureSource"] = "BrowserTracker",
                ["url"] = scenario.Url,
                ["browserType"] = scenario.BrowserType,
                ["tabCount"] = Random.Shared.Next(1, 10)
            }
        };
    }

    private async Task<EventDto> SimulateTeamsTrackerCaptureAsync(TeamsTrackingScenario scenario)
    {
        return new EventDto
        {
            Id = Guid.NewGuid(),
            AgentId = scenario.AgentId,
            ActivityType = ActivityType.TeamsActivity,
            ProcessName = scenario.ProcessName,
            WindowTitle = scenario.WindowTitle,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["captureSource"] = "TeamsTracker",
                ["meetingId"] = scenario.MeetingId,
                ["meetingTitle"] = scenario.MeetingTitle,
                ["participantCount"] = scenario.ParticipantCount,
                ["meetingType"] = "video"
            }
        };
    }

    private async Task<ScreenshotDto> SimulateScreenshotCaptureAsync(ScreenshotCaptureScenario scenario)
    {
        var imageData = GenerateTestImageData(scenario.Width, scenario.Height, scenario.Format);
        
        return new ScreenshotDto
        {
            Id = Guid.NewGuid(),
            AgentId = scenario.AgentId,
            Timestamp = DateTime.UtcNow,
            Data = imageData,
            Format = scenario.Format,
            Width = scenario.Width,
            Height = scenario.Height,
            Quality = scenario.Quality
        };
    }

    private byte[] GenerateTestImageData(int width, int height, string format)
    {
        // Gerar dados de imagem de teste (PNG 1x1)
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

    private async Task<List<EventDto>> GetEventsFromDatabaseAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}&source=database");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<EventDto>>() ?? new List<EventDto>();
    }

    private async Task<List<EventDto>> GetEventsFromFrontendAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<EventDto>>() ?? new List<EventDto>();
    }

    private async Task<List<EventDto>> GetBrowserEventsAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}&type=browser");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<EventDto>>() ?? new List<EventDto>();
    }

    private async Task<List<EventDto>> GetTeamsEventsAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}&type=teams");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<EventDto>>() ?? new List<EventDto>();
    }

    private async Task<bool> VerifyScreenshotInMinIOAsync(Guid screenshotId)
    {
        try
        {
            var objectName = $"screenshots/{screenshotId}.png";
            await _fixture.MinioClient.StatObjectAsync(
                new Minio.DataModel.Args.StatObjectArgs()
                    .WithBucket("screenshots")
                    .WithObject(objectName));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<ScreenshotDto> GetScreenshotMetadataAsync(Guid screenshotId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/screenshots/{screenshotId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScreenshotDto>();
    }

    private async Task<EventDto> GetCachedEventAsync(Guid eventId)
    {
        var database = _fixture.RedisConnection.GetDatabase();
        var cachedData = await database.StringGetAsync($"event:{eventId}");
        
        if (cachedData.HasValue)
        {
            return JsonSerializer.Deserialize<EventDto>(cachedData);
        }
        
        return null;
    }
}

public class WindowTrackingScenario
{
    public Guid AgentId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public ActivityType ExpectedActivityType { get; set; }
}

public class BrowserTrackingScenario
{
    public Guid AgentId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string BrowserType { get; set; } = string.Empty;
}

public class TeamsTrackingScenario
{
    public Guid AgentId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string MeetingId { get; set; } = string.Empty;
    public string MeetingTitle { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
}

public class ScreenshotCaptureScenario
{
    public Guid AgentId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Quality { get; set; }
    public string Format { get; set; } = string.Empty;
}

public class RealTimeUpdateScenario
{
    public Guid AgentId { get; set; }
    public TimeSpan UpdateInterval { get; set; }
    public int TotalUpdates { get; set; }
}