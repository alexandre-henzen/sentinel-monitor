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
/// Testes de integração entre componentes do sistema EAM
/// Valida comunicação entre Agente, API e Frontend
/// </summary>
[Collection("Integration")]
public class ComponentIntegrationTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<ComponentIntegrationTests> _logger;

    public ComponentIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = _fixture.Logger<ComponentIntegrationTests>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.StartInfrastructureAsync();
        await _fixture.StartApiAsync();
        await Task.Delay(5000); // Aguarda estabilização
    }

    public async Task DisposeAsync()
    {
        await _fixture.StopApiAsync();
        await _fixture.StopInfrastructureAsync();
    }

    [Fact]
    public async Task AgentToAPI_ShouldCommunicateViaHTTP()
    {
        // Arrange
        var testAgent = new AgentDto
        {
            Id = Guid.NewGuid(),
            MachineName = "TEST-MACHINE",
            Username = "test-user",
            Version = "5.0.0",
            Status = AgentStatus.Online,
            LastHeartbeat = DateTime.UtcNow
        };

        _output.WriteLine($"Testando comunicação Agente->API com agente {testAgent.Id}");

        // Act 1: Registrar agente
        var registerResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/agents/register", testAgent);
        
        // Assert 1: Registro bem-sucedido
        registerResponse.EnsureSuccessStatusCode();
        var registeredAgent = await registerResponse.Content.ReadFromJsonAsync<AgentDto>();
        registeredAgent.Should().NotBeNull();
        registeredAgent.Id.Should().Be(testAgent.Id);

        // Act 2: Enviar heartbeat
        var heartbeatResponse = await _fixture.HttpClient.PostAsync($"/api/agents/{testAgent.Id}/heartbeat", null);
        
        // Assert 2: Heartbeat aceito
        heartbeatResponse.EnsureSuccessStatusCode();

        // Act 3: Enviar eventos
        var testEvent = new EventDto
        {
            Id = Guid.NewGuid(),
            AgentId = testAgent.Id,
            ActivityType = ActivityType.WindowChange,
            ProcessName = "notepad",
            WindowTitle = "Test Document",
            Timestamp = DateTime.UtcNow
        };

        var eventResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/events", testEvent);
        
        // Assert 3: Evento aceito
        eventResponse.EnsureSuccessStatusCode();

        // Act 4: Verificar dados na API
        var getEventsResponse = await _fixture.HttpClient.GetAsync($"/api/events?agentId={testAgent.Id}");
        var events = await getEventsResponse.Content.ReadFromJsonAsync<List<EventDto>>();
        
        // Assert 4: Evento armazenado
        events.Should().NotBeEmpty();
        events.Should().Contain(e => e.Id == testEvent.Id);
    }

    [Fact]
    public async Task AgentToAPI_ShouldHandleNDJSONStreaming()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var events = new List<EventDto>();
        
        for (int i = 0; i < 100; i++)
        {
            events.Add(new EventDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ActivityType = ActivityType.KeyPress,
                ProcessName = "test-app",
                WindowTitle = $"Test Window {i}",
                Timestamp = DateTime.UtcNow.AddSeconds(i)
            });
        }

        _output.WriteLine($"Testando streaming NDJSON com {events.Count} eventos");

        // Act: Enviar eventos via NDJSON
        var ndjsonContent = string.Join("\n", events.Select(e => JsonSerializer.Serialize(e)));
        var content = new StringContent(ndjsonContent, System.Text.Encoding.UTF8, "application/x-ndjson");
        
        var response = await _fixture.HttpClient.PostAsync("/api/events/batch", content);
        
        // Assert: Batch aceito
        response.EnsureSuccessStatusCode();
        
        // Verificar se todos os eventos foram processados
        await Task.Delay(2000); // Aguarda processamento
        
        var getResponse = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}");
        var storedEvents = await getResponse.Content.ReadFromJsonAsync<List<EventDto>>();
        
        storedEvents.Should().HaveCount(events.Count);
    }

    [Fact]
    public async Task APIToFrontend_ShouldProvideRESTEndpoints()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        await SeedTestDataAsync(agentId);

        _output.WriteLine("Testando endpoints REST da API para Frontend");

        // Act & Assert: Dashboard endpoint
        var dashboardResponse = await _fixture.HttpClient.GetAsync("/api/dashboard");
        dashboardResponse.EnsureSuccessStatusCode();
        
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<DashboardData>();
        dashboard.Should().NotBeNull();
        dashboard.TotalAgents.Should().BeGreaterThan(0);

        // Act & Assert: Agents endpoint
        var agentsResponse = await _fixture.HttpClient.GetAsync("/api/agents");
        agentsResponse.EnsureSuccessStatusCode();
        
        var agents = await agentsResponse.Content.ReadFromJsonAsync<List<AgentDto>>();
        agents.Should().NotBeEmpty();

        // Act & Assert: Events endpoint com filtros
        var eventsResponse = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}&limit=10");
        eventsResponse.EnsureSuccessStatusCode();
        
        var events = await eventsResponse.Content.ReadFromJsonAsync<List<EventDto>>();
        events.Should().NotBeEmpty();
        events.Should().HaveCountLessOrEqualTo(10);

        // Act & Assert: Timeline endpoint
        var timelineResponse = await _fixture.HttpClient.GetAsync($"/api/timeline?agentId={agentId}&date={DateTime.UtcNow:yyyy-MM-dd}");
        timelineResponse.EnsureSuccessStatusCode();
        
        var timeline = await timelineResponse.Content.ReadFromJsonAsync<List<TimelineEvent>>();
        timeline.Should().NotBeEmpty();
    }

    [Fact]
    public async Task APIToDatabase_ShouldPersistDataCorrectly()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var testEvents = new List<EventDto>();
        
        for (int i = 0; i < 10; i++)
        {
            testEvents.Add(new EventDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ActivityType = ActivityType.WindowChange,
                ProcessName = $"process-{i}",
                WindowTitle = $"Window {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            });
        }

        _output.WriteLine("Testando persistência de dados no PostgreSQL");

        // Act: Inserir eventos
        foreach (var evt in testEvents)
        {
            var response = await _fixture.HttpClient.PostAsJsonAsync("/api/events", evt);
            response.EnsureSuccessStatusCode();
        }

        await Task.Delay(2000); // Aguarda persistência

        // Assert: Verificar dados no banco
        var storedEvents = await GetEventsFromDatabaseAsync(agentId);
        storedEvents.Should().HaveCount(testEvents.Count);
        
        foreach (var originalEvent in testEvents)
        {
            var storedEvent = storedEvents.FirstOrDefault(e => e.Id == originalEvent.Id);
            storedEvent.Should().NotBeNull();
            storedEvent.ProcessName.Should().Be(originalEvent.ProcessName);
            storedEvent.WindowTitle.Should().Be(originalEvent.WindowTitle);
        }
    }

    [Fact]
    public async Task APIToRedis_ShouldCacheDataCorrectly()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = new AgentDto
        {
            Id = agentId,
            MachineName = "CACHE-TEST",
            Username = "cache-user",
            Version = "5.0.0",
            Status = AgentStatus.Online,
            LastHeartbeat = DateTime.UtcNow
        };

        _output.WriteLine("Testando cache Redis");

        // Act: Registrar agente (deve ser cacheado)
        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/agents/register", agent);
        response.EnsureSuccessStatusCode();

        await Task.Delay(1000); // Aguarda cache

        // Assert: Verificar cache
        var database = _fixture.RedisConnection.GetDatabase();
        var cachedAgent = await database.StringGetAsync($"agent:{agentId}");
        cachedAgent.Should().NotBeNull();
        
        var deserializedAgent = JsonSerializer.Deserialize<AgentDto>(cachedAgent);
        deserializedAgent.Should().NotBeNull();
        deserializedAgent.Id.Should().Be(agentId);
    }

    [Fact]
    public async Task APIToMinIO_ShouldStoreScreenshots()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var screenshotData = GenerateTestScreenshot();
        
        var screenshot = new ScreenshotDto
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Timestamp = DateTime.UtcNow,
            Data = screenshotData,
            Format = "png"
        };

        _output.WriteLine("Testando armazenamento de screenshots no MinIO");

        // Act: Enviar screenshot
        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/screenshots", screenshot);
        response.EnsureSuccessStatusCode();

        await Task.Delay(2000); // Aguarda upload

        // Assert: Verificar no MinIO
        var objectName = $"screenshots/{screenshot.Id}.png";
        var bucketExists = await _fixture.MinioClient.BucketExistsAsync(
            new Minio.DataModel.Args.BucketExistsArgs().WithBucket("screenshots"));
        
        bucketExists.Should().BeTrue();

        var objectExists = await ObjectExistsAsync("screenshots", objectName);
        objectExists.Should().BeTrue();
    }

    [Fact]
    public async Task ComponentFailover_ShouldMaintainServiceAvailability()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        
        _output.WriteLine("Testando failover entre componentes");

        // Act 1: Simular falha do Redis
        await _fixture.RedisContainer.StopAsync();
        
        // System should still work without cache
        var response = await _fixture.HttpClient.GetAsync("/api/agents");
        response.EnsureSuccessStatusCode();
        
        // Act 2: Restaurar Redis
        await _fixture.RedisContainer.StartAsync();
        await Task.Delay(5000); // Aguarda recuperação
        
        // Act 3: Simular falha do MinIO
        await _fixture.MinioContainer.StopAsync();
        
        // Events should still be processed (screenshots may fail)
        var testEvent = new EventDto
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            ActivityType = ActivityType.WindowChange,
            ProcessName = "test-failover",
            WindowTitle = "Failover Test",
            Timestamp = DateTime.UtcNow
        };

        var eventResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/events", testEvent);
        eventResponse.EnsureSuccessStatusCode();
        
        // Act 4: Restaurar MinIO
        await _fixture.MinioContainer.StartAsync();
        await Task.Delay(5000);
        
        // Assert: Sistema deve estar funcionando completamente
        var healthResponse = await _fixture.HttpClient.GetAsync("/health");
        healthResponse.EnsureSuccessStatusCode();
    }

    private async Task SeedTestDataAsync(Guid agentId)
    {
        // Registrar agente
        var agent = new AgentDto
        {
            Id = agentId,
            MachineName = "TEST-SEED",
            Username = "seed-user",
            Version = "5.0.0",
            Status = AgentStatus.Online,
            LastHeartbeat = DateTime.UtcNow
        };

        await _fixture.HttpClient.PostAsJsonAsync("/api/agents/register", agent);

        // Adicionar eventos
        var events = new List<EventDto>();
        for (int i = 0; i < 5; i++)
        {
            events.Add(new EventDto
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ActivityType = ActivityType.WindowChange,
                ProcessName = $"seed-process-{i}",
                WindowTitle = $"Seed Window {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            });
        }

        foreach (var evt in events)
        {
            await _fixture.HttpClient.PostAsJsonAsync("/api/events", evt);
        }

        await Task.Delay(2000); // Aguarda processamento
    }

    private async Task<List<EventDto>> GetEventsFromDatabaseAsync(Guid agentId)
    {
        // Simular consulta direta ao banco
        var response = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<List<EventDto>>() ?? new List<EventDto>();
    }

    private byte[] GenerateTestScreenshot()
    {
        // Gerar dados de imagem PNG simples (1x1 pixel)
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

    private async Task<bool> ObjectExistsAsync(string bucketName, string objectName)
    {
        try
        {
            await _fixture.MinioClient.StatObjectAsync(
                new Minio.DataModel.Args.StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName));
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class DashboardData
{
    public int TotalAgents { get; set; }
    public int OnlineAgents { get; set; }
    public int TotalEvents { get; set; }
    public List<RecentActivity> RecentActivities { get; set; } = new();
}

public class RecentActivity
{
    public Guid Id { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class TimelineEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}