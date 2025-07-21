using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using EAM.Agent.Core.Models;
using EAM.Agent.Core.Data;
using EAM.Agent.Core.Windows;
using System.Diagnostics;
using System.Text;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Tracker focado em sessões de uso de aplicações (tempo efetivo)
/// </summary>
public class SessionTracker : ITracker, IDisposable
{
    public string Name => "SessionTracker";
    public bool IsEnabled { get; private set; }
    public int IntervalSeconds => 2;

    public event EventHandler<EAM.Agent.Core.Models.ActivityEvent>? EventCaptured;
    public event EventHandler<SessionEvent>? SessionStarted;
    public event EventHandler<SessionEvent>? SessionEnded;

    private readonly ILogger<SessionTracker> _logger;
    private readonly AgentDbContext _dbContext;
    private readonly TrackerConfiguration _configuration;
    private readonly System.Threading.Timer _monitorTimer;
    
    private SessionEvent? _currentSession;
    private string _lastActiveApplication = string.Empty;
    private string _lastActiveWindowTitle = string.Empty;
    private DateTime _lastActivity = DateTime.UtcNow;

    // Lista de aplicações relevantes para monitoramento
    private readonly HashSet<string> _relevantApplications = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navegadores
        "msedge", "chrome", "firefox", "opera", "brave",
        
        // Desenvolvimento
        "code", "devenv", "rider", "idea64", "studio64", "notepad++", "sublime_text",
        
        // Office/Produtividade
        "winword", "excel", "powerpnt", "outlook", "onenote", "teams",
        
        // Design/Criatividade
        "photoshop", "illustrator", "figma", "sketch", "canva",
        
        // Comunicação
        "slack", "discord", "whatsapp", "telegram", "zoom",
        
        // Sistema (selecionados)
        "explorer", "calculator", "notepad", "mspaint",
        
        // Outros produtivos
        "notion", "obsidian", "evernote", "trello", "asana"
    };

    public SessionTracker(
        ILogger<SessionTracker> logger,
        AgentDbContext dbContext,
        IOptions<TrackerConfiguration> configuration)
    {
        _logger = logger;
        _dbContext = dbContext;
        _configuration = configuration.Value;
        
        // Timer para verificar aplicação ativa a cada 2 segundos
        _monitorTimer = new System.Threading.Timer(CheckActiveApplication, null, Timeout.Infinite, 2000);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("🚀 SessionTracker.StartAsync() CHAMADO");
            _logger.LogInformation("Iniciando SessionTracker...");
            
            // Recupera sessão ativa anterior (se houver)
            RecoverActiveSession();
            Console.WriteLine("✅ Sessão ativa recuperada (se existia)");
            
            // Inicia monitoramento
            _monitorTimer.Change(0, 2000);
            IsEnabled = true;
            Console.WriteLine("✅ Timer de monitoramento iniciado (2 segundos)");
            
            Console.WriteLine("✅ SessionTracker INICIADO COM SUCESSO");
            _logger.LogInformation("SessionTracker iniciado com sucesso");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERRO ao iniciar SessionTracker: {ex.Message}");
            _logger.LogError(ex, "Erro ao iniciar SessionTracker");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Parando SessionTracker...");
            
            // Para o timer
            _monitorTimer.Change(Timeout.Infinite, 0);
            
            // Finaliza sessão ativa
            if (_currentSession != null)
            {
                EndCurrentSession();
            }
            
            IsEnabled = false;
            _logger.LogInformation("SessionTracker parado");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar SessionTracker");
            return Task.CompletedTask;
        }
    }

    public Task<IEnumerable<EAM.Agent.Core.Models.ActivityEvent>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        // SessionTracker funciona de forma contínua, não por captura pontual
        // Retorna lista vazia
        return Task.FromResult(Enumerable.Empty<EAM.Agent.Core.Models.ActivityEvent>());
    }

    private void CheckActiveApplication(object? state)
    {
        try
        {
            Console.WriteLine("🔍 CheckActiveApplication() executado");
            
            var foregroundWindow = WindowsApi.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                Console.WriteLine("❌ Nenhuma janela em foreground");
                return;
            }

            // Obtém informações da aplicação ativa
            var processId = WindowsApi.GetWindowThreadProcessId(foregroundWindow, out _);
            if (processId == 0)
            {
                Console.WriteLine("❌ ProcessId = 0");
                return;
            }

            Console.WriteLine($"📱 ProcessId detectado: {processId}");

            // Protege contra processos que podem ter terminado
            Process? process = null;
            try
            {
                process = Process.GetProcessById((int)processId);
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"❌ Processo {processId} não existe mais");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao obter processo {processId}: {ex.Message}");
                _logger.LogDebug(ex, "Erro ao obter processo {ProcessId}", processId);
                return;
            }

            using (process)
            {
                string applicationName;
                try
                {
                    applicationName = process.ProcessName.ToLowerInvariant();
                    Console.WriteLine($"📱 Aplicação detectada: {applicationName}");
                }
                catch (Exception)
                {
                    Console.WriteLine("❌ Não conseguiu obter nome do processo");
                    return;
                }

                var titleBuffer = new StringBuilder(256);
                var windowTitleLength = WindowsApi.GetWindowText(foregroundWindow, titleBuffer, 256);
                var windowTitle = titleBuffer.ToString();
                Console.WriteLine($"🪟 Título da janela: {windowTitle}");

                // Verifica se é uma aplicação relevante
                bool isRelevant = IsRelevantApplication(applicationName, windowTitle);
                Console.WriteLine($"✅ Aplicação relevante: {isRelevant}");
                
                if (!isRelevant)
                {
                    Console.WriteLine("⏭️ Aplicação não relevante, ignorando...");
                    // Se estava em uma sessão de app relevante, finaliza
                    if (_currentSession != null)
                    {
                        Console.WriteLine("🔄 Finalizando sessão ativa (app não relevante)");
                        EndCurrentSession();
                    }
                    return;
                }

                // Verifica se mudou de aplicação
                bool appChanged = _lastActiveApplication != applicationName || _lastActiveWindowTitle != windowTitle;
                Console.WriteLine($"🔄 Aplicação mudou: {appChanged} (anterior: {_lastActiveApplication})");
                
                if (appChanged)
                {
                    // Finaliza sessão anterior se houver
                    if (_currentSession != null)
                    {
                        Console.WriteLine("🔄 Finalizando sessão anterior");
                        EndCurrentSession();
                    }

                    // Inicia nova sessão
                    Console.WriteLine($"🆕 Iniciando nova sessão para: {applicationName}");
                    StartNewSession(applicationName, windowTitle, process);
                    
                    _lastActiveApplication = applicationName;
                    _lastActiveWindowTitle = windowTitle;
                }
                else
                {
                    Console.WriteLine("✅ Mantendo sessão atual");
                }

                _lastActivity = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERRO em CheckActiveApplication: {ex.Message}");
            _logger.LogDebug(ex, "Erro ao verificar aplicação ativa");
        }
    }

    private bool IsRelevantApplication(string applicationName, string windowTitle)
    {
        // Verifica lista de aplicações relevantes
        if (_relevantApplications.Contains(applicationName))
            return true;

        // Filtros especiais
        if (applicationName == "applicationframehost" && !string.IsNullOrEmpty(windowTitle))
        {
            // Apps UWP do Windows 10/11
            return windowTitle.Contains("Microsoft Store") || 
                   windowTitle.Contains("Calculator") ||
                   windowTitle.Contains("Paint");
        }

        // Exclui explicitamente processos de sistema
        var systemProcesses = new[] { "svchost", "dwm", "winlogon", "csrss", "services", 
                                     "lsass", "runtimebroker", "taskmgr" };
        
        return !systemProcesses.Any(sys => applicationName.Contains(sys));
    }

    private void StartNewSession(string applicationName, string windowTitle, Process process)
    {
        try
        {
            Console.WriteLine($"🆕 StartNewSession CHAMADO para: {applicationName}");
            
            var sessionType = DetermineSessionType(applicationName);
            var category = DetermineProductivityCategory(applicationName, windowTitle);
            
            Console.WriteLine($"📊 SessionType: {sessionType}, Category: {category}");
            
            _currentSession = new SessionEvent
            {
                AgentId = Guid.Parse("00000000-0000-0000-0000-000000000001"), // Mock agent ID
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"), // Mock user ID
                ApplicationName = applicationName,
                ApplicationPath = GetSafeExecutablePath(process),
                WindowTitle = windowTitle,
                SessionType = sessionType,
                Category = category,
                ProductivityScore = CalculateProductivityScore(category),
                StartTime = DateTime.UtcNow,
                IsActive = true
            };

            Console.WriteLine($"✅ SessionEvent criado: ID={_currentSession.Id}");

            // Extrai URL se for navegador
            if (sessionType == SessionType.Browser)
            {
                ExtractBrowserInfo(_currentSession, windowTitle);
                Console.WriteLine($"🌐 URL extraída: {_currentSession.Url}");
            }

            // Salva no banco
            Console.WriteLine("💾 Salvando no banco...");
            _dbContext.SessionEvents.Add(_currentSession);
            _dbContext.SaveChanges();
            Console.WriteLine("✅ SESSÃO SALVA NO BANCO COM SUCESSO!");

            _logger.LogInformation("Nova sessão iniciada: {App} - {Title} ({Type})",
                applicationName, windowTitle, sessionType);

            // Dispara evento
            SessionStarted?.Invoke(this, _currentSession);
            Console.WriteLine("✅ Evento SessionStarted disparado");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERRO ao iniciar nova sessão para {applicationName}: {ex.Message}");
            _logger.LogError(ex, "Erro ao iniciar nova sessão para {App}", applicationName);
        }
    }

    private void EndCurrentSession()
    {
        try
        {
            if (_currentSession == null) return;

            _currentSession.EndTime = DateTime.UtcNow;
            _currentSession.IsActive = false;
            _currentSession.UpdatedAt = DateTime.UtcNow;

            _dbContext.SessionEvents.Update(_currentSession);
            _dbContext.SaveChanges();

            _logger.LogInformation("Sessão finalizada: {App} - Duração: {Duration}s", 
                _currentSession.ApplicationName, _currentSession.DurationSeconds);

            // Dispara evento
            SessionEnded?.Invoke(this, _currentSession);

            _currentSession = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar sessão");
        }
    }

    private void RecoverActiveSession()
    {
        try
        {
            // Busca sessão ativa não finalizada
            _currentSession = _dbContext.SessionEvents
                .Where(s => s.IsActive && s.EndTime == null)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefault();

            if (_currentSession != null)
            {
                _logger.LogInformation("Sessão ativa recuperada: {App}", _currentSession.ApplicationName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao recuperar sessão ativa");
        }
    }

    private static SessionType DetermineSessionType(string applicationName)
    {
        return applicationName.ToLowerInvariant() switch
        {
            "msedge" or "chrome" or "firefox" or "opera" or "brave" => SessionType.Browser,
            "teams" => SessionType.Teams,
            "code" or "devenv" or "rider" or "idea64" => SessionType.Development,
            "winword" or "excel" or "powerpnt" or "outlook" => SessionType.Office,
            "photoshop" or "illustrator" or "figma" => SessionType.Design,
            "explorer" or "calculator" or "notepad" => SessionType.System,
            _ => SessionType.Application
        };
    }

    private static EAM.Agent.Core.Models.ProductivityCategory DetermineProductivityCategory(string applicationName, string windowTitle)
    {
        // Lógica simplificada - pode ser expandida
        var productiveApps = new[] { "code", "winword", "excel", "teams", "outlook" };
        var distractionApps = new[] { "game", "youtube", "facebook", "instagram" };

        if (productiveApps.Any(app => applicationName.Contains(app)))
            return EAM.Agent.Core.Models.ProductivityCategory.Productive;
        
        if (distractionApps.Any(app => windowTitle.ToLowerInvariant().Contains(app)))
            return EAM.Agent.Core.Models.ProductivityCategory.Distraction;

        return EAM.Agent.Core.Models.ProductivityCategory.Neutral;
    }

    private static int CalculateProductivityScore(EAM.Agent.Core.Models.ProductivityCategory category)
    {
        return category switch
        {
            EAM.Agent.Core.Models.ProductivityCategory.Productive => 80,
            EAM.Agent.Core.Models.ProductivityCategory.Neutral => 50,
            EAM.Agent.Core.Models.ProductivityCategory.Distraction => 20,
            _ => 50
        };
    }

    private static void ExtractBrowserInfo(SessionEvent session, string windowTitle)
    {
        // Extração simples de URL do título (melhorar futuramente)
        if (windowTitle.Contains("http"))
        {
            try
            {
                var urlStart = windowTitle.IndexOf("http");
                var urlEnd = windowTitle.IndexOf(' ', urlStart);
                if (urlEnd == -1) urlEnd = windowTitle.Length;
                
                session.Url = windowTitle.Substring(urlStart, urlEnd - urlStart);
                session.Domain = new Uri(session.Url).Host;
            }
            catch
            {
                // Ignore URL extraction errors
            }
        }
    }

    private static string? GetSafeExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _monitorTimer?.Dispose();
        if (_currentSession != null)
        {
            EndCurrentSession();
        }
    }
}