using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using EAM.Agent.Core.Models;
using EAM.Agent.Core.Data;
using System.Diagnostics;
using ActivityEvent = EAM.Agent.Core.Models.ActivityEvent;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Serviço principal que coordena todos os trackers do agente
/// </summary>
public class AgentTrackerService : BackgroundService
{
    private readonly ILogger<AgentTrackerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentConfiguration _configuration;
    private readonly List<ITracker> _trackers = new();
    private readonly System.Threading.Timer _syncTimer;
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly System.Threading.Timer _checkpointTimer; // Timer para checkpoint manual
    private IServiceScope? _trackersScope; // Scope permanente para os trackers

    private bool _isInitialized = false;
    private readonly object _lockObject = new object();

    public AgentTrackerService(
        ILogger<AgentTrackerService> logger,
        IServiceProvider serviceProvider,
        IOptions<AgentConfiguration> configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration.Value;
        
        // Timer para sincronização de dados (60 segundos)
        _syncTimer = new System.Threading.Timer(
            SyncDataAsync,
            null,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(60));
        
        // Timer para limpeza de dados antigos (1 hora)
        _cleanupTimer = new System.Threading.Timer(
            CleanupOldDataAsync,
            null,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1));
        
        // Timer para checkpoint manual WAL (30 segundos)
        _checkpointTimer = new System.Threading.Timer(
            CheckpointWalAsync,
            null,
            30000, // 30 segundos em millisegundos
            30000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await InitializeTrackersAsync(stoppingToken);
            
            _logger.LogInformation("AgentTrackerService iniciado com {TrackerCount} trackers", _trackers.Count);
            
            // Monitora o token de cancelamento
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                
                // Verifica saúde dos trackers
                await CheckTrackersHealthAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AgentTrackerService foi cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro crítico no AgentTrackerService");
            throw;
        }
        finally
        {
            await StopTrackersAsync();
        }
    }

    private async Task InitializeTrackersAsync(CancellationToken cancellationToken)
    {
        lock (_lockObject)
        {
            if (_isInitialized)
                return;
            
            _isInitialized = true;
        }

        try
        {
            // Inicializa banco de dados
            await InitializeDatabaseAsync(cancellationToken);
            
            // Cria e configura trackers
            await CreateTrackersAsync(cancellationToken);
            
            // Inicia todos os trackers
            await StartTrackersAsync(cancellationToken);
            
            _logger.LogInformation("Todos os trackers foram inicializados com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar trackers");
            throw;
        }
    }

    private async Task InitializeDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
            
            // Garante que o banco existe
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            
            _logger.LogInformation("Banco de dados SQLite inicializado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar banco de dados");
            throw;
        }
    }

    private async Task CreateTrackersAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine("=== INICIANDO CRIAÇÃO DOS TRACKERS ===");
            _logger.LogInformation("=== INICIANDO CRIAÇÃO DOS TRACKERS ===");
            
            // CORREÇÃO: Criar scope permanente para os trackers (resolve problema DI)
            _trackersScope = _serviceProvider.CreateScope();
            Console.WriteLine("✅ Scope permanente criado para trackers");

            // SessionTracker - NOVO: Focado em tempo de uso de aplicações relevantes
            var sessionTracker = _trackersScope.ServiceProvider.GetRequiredService<SessionTracker>();
            _trackers.Add(sessionTracker);
            Console.WriteLine($"✅ SessionTracker criado: {sessionTracker.Name}");
            _logger.LogInformation("SessionTracker criado (foco em tempo de uso): {TrackerName}", sessionTracker.Name);

            // BrowserTracker - REATIVADO: Necessário para capturar abas do browser
            if (_configuration.BrowserTracker.Enabled)
            {
                var browserTracker = _trackersScope.ServiceProvider.GetRequiredService<BrowserTracker>();
                browserTracker.EventCaptured += OnEventCaptured;
                _trackers.Add(browserTracker);
                Console.WriteLine($"✅ BrowserTracker criado: {browserTracker.Name} (para capturar abas)");
                _logger.LogInformation("BrowserTracker criado (para abas): {TrackerName}", browserTracker.Name);
            }
            else
            {
                Console.WriteLine("⚠️  BrowserTracker desabilitado na configuração");
            }

            // TeamsTracker - IMPORTANTE para ambiente corporativo brasileiro
            if (_configuration.TeamsTracker.Enabled)
            {
                var teamsTracker = _trackersScope.ServiceProvider.GetRequiredService<TeamsTracker>();
                teamsTracker.EventCaptured += OnEventCaptured;
                _trackers.Add(teamsTracker);
                Console.WriteLine($"✅ TeamsTracker criado: {teamsTracker.Name} (com suporte português)");
                _logger.LogInformation("TeamsTracker criado (suporte português): {TrackerName}", teamsTracker.Name);
            }
            else
            {
                Console.WriteLine("⚠️  TeamsTracker desabilitado na configuração");
            }

            // ScreenshotCapturer - mantém apenas se habilitado (sem event handler para evitar conflitos)
            if (_configuration.ScreenshotCapturer.Enabled)
            {
                var screenshotCapturer = _trackersScope.ServiceProvider.GetRequiredService<ScreenshotCapturer>();
                // REMOVIDO: screenshotCapturer.EventCaptured += OnEventCaptured; // Causa conflitos de ID
                _trackers.Add(screenshotCapturer);
                Console.WriteLine($"✅ ScreenshotCapturer criado: {screenshotCapturer.Name} (sem event handler)");
                _logger.LogInformation("ScreenshotCapturer criado (sem event handler): {TrackerName}", screenshotCapturer.Name);
            }
            else
            {
                Console.WriteLine("⚠️  ScreenshotCapturer desabilitado na configuração");
            }

            Console.WriteLine($"✅ Total de trackers criados: {_trackers.Count}");
            _logger.LogInformation("Total de trackers criados: {TrackerCount}", _trackers.Count);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERRO ao criar trackers: {ex.Message}");
            _logger.LogError(ex, "Erro ao criar trackers");
            throw;
        }
    }

    private async Task StartTrackersAsync(CancellationToken cancellationToken)
    {
        var startTasks = new List<Task>();
        
        foreach (var tracker in _trackers)
        {
            startTasks.Add(StartTrackerAsync(tracker, cancellationToken));
        }
        
        await Task.WhenAll(startTasks);
    }

    private async Task StartTrackerAsync(ITracker tracker, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"🚀 INICIANDO TRACKER: {tracker.Name}");
            await tracker.StartAsync(cancellationToken);
            Console.WriteLine($"✅ TRACKER INICIADO COM SUCESSO: {tracker.Name} (IsEnabled: {tracker.IsEnabled})");
            _logger.LogInformation("{TrackerName} iniciado (IsEnabled: {IsEnabled})", tracker.Name, tracker.IsEnabled);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERRO ao iniciar {tracker.Name}: {ex.Message}");
            _logger.LogError(ex, "Erro ao iniciar {TrackerName}", tracker.Name);
        }
    }

    private async Task StopTrackersAsync()
    {
        _logger.LogInformation("Parando todos os trackers...");
        
        var stopTasks = new List<Task>();
        
        foreach (var tracker in _trackers)
        {
            stopTasks.Add(StopTrackerAsync(tracker));
        }
        
        await Task.WhenAll(stopTasks);
        
        // Limpa lista de trackers
        _trackers.Clear();
        
        _logger.LogInformation("Todos os trackers foram parados");
    }

    private async Task StopTrackerAsync(ITracker tracker)
    {
        try
        {
            await tracker.StopAsync();
            _logger.LogInformation("{TrackerName} parado", tracker.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar {TrackerName}", tracker.Name);
        }
    }

    private async Task CheckTrackersHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var unhealthyTrackers = _trackers.Where(t => !t.IsEnabled).ToList();
            
            if (unhealthyTrackers.Any())
            {
                _logger.LogWarning("Trackers não saudáveis detectados: {TrackerNames}", 
                    string.Join(", ", unhealthyTrackers.Select(t => t.Name)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar saúde dos trackers");
        }
    }

    private async void OnEventCaptured(object? sender, ActivityEvent activityEvent)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scoringEngine = scope.ServiceProvider.GetRequiredService<ScoringEngine>();
            var repository = scope.ServiceProvider.GetRequiredService<IActivityEventRepository>();
            
            // Aplica pontuação de produtividade
            await scoringEngine.ScoreActivityEventAsync(activityEvent);
            
            // CORREÇÃO CRÍTICA: Salvar evento no banco de dados
            await repository.AddActivityEventAsync(activityEvent);
            
            _logger.LogDebug("Evento capturado, pontuado e salvo: {EventType} - {Application} - Score: {Score}",
                activityEvent.Type, activityEvent.Application, activityEvent.ProductivityScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento capturado");
        }
    }

    private async void SyncDataAsync(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IActivityEventRepository>();
            
            _logger.LogDebug("Iniciando sincronização de dados...");
            
            // Obtém eventos não sincronizados
            var unsyncedEvents = await repository.GetUnsyncedEventsAsync(100);
            
            if (unsyncedEvents.Any())
            {
                _logger.LogInformation("Sincronizando {EventCount} eventos não sincronizados", unsyncedEvents.Count());
                
                // Aqui seria implementada a sincronização com a API
                // Por enquanto, apenas marca como sincronizado
                var eventIds = unsyncedEvents.Select(e => e.Id).ToList();
                await repository.MarkEventsSyncedAsync(eventIds);
                
                _logger.LogInformation("Sincronização concluída");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante sincronização de dados");
        }
    }

    private async void CleanupOldDataAsync(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IActivityEventRepository>();
            
            _logger.LogDebug("Iniciando limpeza de dados antigos...");
            
            // Remove eventos sincronizados com mais de 7 dias
            var cutoffDate = DateTime.UtcNow.AddDays(-_configuration.RetentionDays);
            await repository.CleanupOldEventsAsync(cutoffDate);
            
            _logger.LogInformation("Limpeza de dados antigos concluída");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante limpeza de dados antigos");
        }
    }

    private async void CheckpointWalAsync(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
            
            _logger.LogDebug("Executando checkpoint manual do WAL...");
            
            // Força checkpoint do WAL
            dbContext.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(PASSIVE)");
            
            _logger.LogDebug("Checkpoint manual do WAL concluído");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro durante checkpoint manual do WAL");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Parando AgentTrackerService...");
        
        // Para os timers
        _syncTimer?.Change(Timeout.Infinite, 0);
        _cleanupTimer?.Change(Timeout.Infinite, 0);
        _checkpointTimer?.Change(Timeout.Infinite, 0);
        
        // Para o serviço base
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("AgentTrackerService parado");
    }

    public override void Dispose()
    {
        _syncTimer?.Dispose();
        _cleanupTimer?.Dispose();
        _checkpointTimer?.Dispose();
        
        // Dispose dos trackers
        foreach (var tracker in _trackers)
        {
            if (tracker is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        // Dispose do scope permanente dos trackers
        _trackersScope?.Dispose();
        
        base.Dispose();
    }
}

/// <summary>
/// Configuração do agente
/// </summary>
public class AgentConfiguration
{
    public TrackerConfiguration WindowTracker { get; set; } = new();
    public TrackerConfiguration BrowserTracker { get; set; } = new();
    public TrackerConfiguration TeamsTracker { get; set; } = new();
    public TrackerConfiguration ScreenshotCapturer { get; set; } = new();
    public TrackerConfiguration ProcessMonitor { get; set; } = new();
    public int RetentionDays { get; set; } = 7;
    public int SyncIntervalSeconds { get; set; } = 60;
    public int CleanupIntervalHours { get; set; } = 24;
    public bool EnableTelemetry { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
}