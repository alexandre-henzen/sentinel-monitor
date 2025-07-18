using EAM.Agent.Data;
using EAM.Shared.DTOs;
using EAM.Shared.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Net;

namespace EAM.Agent.Services;

public class SyncService
{
    private readonly ILogger<SyncService> _logger;
    private readonly IConfiguration _configuration;
    private readonly DatabaseService _databaseService;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private DateTime _lastSyncTime;
    private bool _isOnline;

    public SyncService(
        ILogger<SyncService> logger,
        IConfiguration configuration,
        DatabaseService databaseService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _databaseService = databaseService;
        _httpClient = httpClientFactory.CreateClient("EAMApi");
        _lastSyncTime = DateTime.UtcNow;
        _isOnline = true;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task SyncAsync()
    {
        try
        {
            await EnsureAuthenticatedAsync();
            
            if (!_isOnline)
            {
                _logger.LogDebug("Skipping sync - offline mode");
                return;
            }

            var events = await _databaseService.GetUnsyncedEventsAsync();
            if (!events.Any())
            {
                _logger.LogDebug("No events to sync");
                return;
            }

            _logger.LogInformation("Sincronizando {Count} eventos", events.Count);
            
            // Converter para DTOs
            var eventDtos = events.Select(ConvertToEventDto).ToList();
            
            // Enviar em lotes NDJSON
            var syncedEventIds = await SendEventsInBatchesAsync(eventDtos);
            
            // Marcar como sincronizados
            if (syncedEventIds.Any())
            {
                await _databaseService.MarkEventsAsSyncedAsync(syncedEventIds);
                _logger.LogInformation("Sincronizados {Count} eventos com sucesso", syncedEventIds.Count);
            }
            
            _lastSyncTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante sincronização");
            await HandleSyncError(ex);
        }
    }

    private async Task<List<long>> SendEventsInBatchesAsync(List<EventDto> events)
    {
        var syncedIds = new List<long>();
        const int batchSize = 100;
        
        for (int i = 0; i < events.Count; i += batchSize)
        {
            var batch = events.Skip(i).Take(batchSize).ToList();
            
            try
            {
                var batchSyncedIds = await SendEventBatchAsync(batch);
                syncedIds.AddRange(batchSyncedIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro enviando lote de eventos {BatchStart}-{BatchEnd}", 
                    i, Math.Min(i + batchSize - 1, events.Count - 1));
                
                // Se um lote falhar, continuar com os próximos
                continue;
            }
        }
        
        return syncedIds;
    }

    private async Task<List<long>> SendEventBatchAsync(List<EventDto> batch)
    {
        var ndjsonContent = CreateNdjsonContent(batch);
        
        using var content = new StringContent(ndjsonContent, Encoding.UTF8, "application/x-ndjson");
        
        var response = await _httpClient.PostAsync(ApiEndpoints.EventsNdjson, content);
        
        if (response.IsSuccessStatusCode)
        {
            // Extrair IDs dos eventos enviados com sucesso
            var eventIds = batch
                .Select(e => e.GetMetadata<long>("local_id"))
                .Where(id => id != 0)
                .ToList();
            
            return eventIds;
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Erro enviando lote de eventos: {StatusCode} - {Error}", 
                response.StatusCode, errorContent);
            
            // Se for erro de autenticação, tentar reautenticar
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await HandleAuthenticationError();
            }
            
            throw new HttpRequestException($"Erro HTTP {response.StatusCode}: {errorContent}");
        }
    }

    private string CreateNdjsonContent(List<EventDto> events)
    {
        var lines = events.Select(evt => JsonSerializer.Serialize(evt, _jsonOptions));
        return string.Join("\n", lines);
    }

    private EventDto ConvertToEventDto(EAM.PluginSDK.ActivityEvent activityEvent)
    {
        // Obter agent ID da configuração
        var agentId = GetAgentId();
        
        var eventDto = new EventDto
        {
            AgentId = agentId,
            EventType = activityEvent.EventType,
            ApplicationName = activityEvent.ApplicationName,
            WindowTitle = activityEvent.WindowTitle,
            Url = activityEvent.Url,
            ProcessName = activityEvent.ProcessName,
            ProcessId = activityEvent.ProcessId,
            DurationSeconds = activityEvent.DurationSeconds,
            ProductivityScore = activityEvent.ProductivityScore,
            EventTimestamp = activityEvent.Timestamp,
            ScreenshotPath = activityEvent.ScreenshotPath,
            Metadata = activityEvent.Metadata,
            CreatedAt = DateTime.UtcNow
        };

        // Preservar ID local para marcação como sincronizado
        if (activityEvent.Metadata?.ContainsKey("local_id") == true)
        {
            eventDto.AddMetadata("local_id", activityEvent.Metadata["local_id"]);
        }

        return eventDto;
    }

    private async Task EnsureAuthenticatedAsync()
    {
        try
        {
            var token = await GetAuthTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro configurando autenticação");
            throw;
        }
    }

    private async Task<string?> GetAuthTokenAsync()
    {
        var tokenPath = _configuration["Security:JwtTokenPath"];
        if (string.IsNullOrEmpty(tokenPath))
            return null;

        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(tokenPath);
            if (File.Exists(expandedPath))
            {
                var token = await File.ReadAllTextAsync(expandedPath);
                return token?.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro carregando token de autenticação");
        }

        return null;
    }

    private Guid GetAgentId()
    {
        var machineId = _configuration["Agent:MachineId"];
        if (string.IsNullOrEmpty(machineId))
        {
            // Gerar ID baseado no nome da máquina
            machineId = Environment.MachineName;
        }

        // Converter para GUID determinístico
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(machineId));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    private async Task HandleSyncError(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            _logger.LogWarning("Erro de conexão durante sync: {Message}", httpEx.Message);
            _isOnline = false;
        }
        else if (ex is TaskCanceledException)
        {
            _logger.LogWarning("Timeout durante sync");
            _isOnline = false;
        }
        else
        {
            _logger.LogError(ex, "Erro inesperado durante sync");
        }
    }

    private async Task HandleAuthenticationError()
    {
        _logger.LogWarning("Erro de autenticação - tentando reautenticar");
        
        try
        {
            // Tentar registrar o agente novamente
            await RegisterAgentAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tentando reautenticar");
        }
    }

    private async Task RegisterAgentAsync()
    {
        try
        {
            var agentDto = new AgentDto
            {
                MachineId = Environment.MachineName,
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                OsVersion = Environment.OSVersion.ToString(),
                AgentVersion = "5.0.0",
                Status = EAM.Shared.Enums.AgentStatus.Active
            };

            var json = JsonSerializer.Serialize(agentDto, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ApiEndpoints.AgentsRegister, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                // Processar resposta e salvar token se necessário
                _logger.LogInformation("Agente registrado com sucesso");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro registrando agente: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro registrando agente");
            throw;
        }
    }

    public async Task SendHeartbeatAsync()
    {
        try
        {
            await EnsureAuthenticatedAsync();
            
            var heartbeatData = new
            {
                AgentId = GetAgentId(),
                Timestamp = DateTime.UtcNow,
                Status = "Active",
                Version = "5.0.0"
            };

            var json = JsonSerializer.Serialize(heartbeatData, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ApiEndpoints.AgentsHeartbeat, content);
            
            if (response.IsSuccessStatusCode)
            {
                _isOnline = true;
                _logger.LogDebug("Heartbeat enviado com sucesso");
            }
            else
            {
                _logger.LogWarning("Erro enviando heartbeat: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro enviando heartbeat");
            _isOnline = false;
        }
    }

    public bool IsOnline => _isOnline;
    public DateTime LastSyncTime => _lastSyncTime;
}