using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EAM.Agent.Core.Models;
using EAM.Agent.Core.Windows;
using EAM.Agent.Core.Data;
using System.Threading;
using System.Text.RegularExpressions;
using ActivityEvent = EAM.Agent.Core.Models.ActivityEvent;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Tracker para captura de URLs e navegação em navegadores
/// </summary>
public class BrowserTracker : ITracker, IDisposable
{
    private readonly ILogger<BrowserTracker> _logger;
    private readonly IActivityEventRepository _repository;
    private readonly TrackerConfiguration _configuration;
    private readonly System.Threading.Timer _timer;
    private readonly Guid _agentId;
    private readonly Guid _userId;
    
    private string _lastUrl = string.Empty;
    private string _lastPageTitle = string.Empty;
    private BrowserType _lastBrowserType = BrowserType.Other;
    private DateTime _lastCaptureTime = DateTime.MinValue;
    private bool _isRunning = false;
    private bool _disposed = false;

    private readonly Dictionary<string, BrowserType> _browserProcesses = new()
    {
        { "chrome", BrowserType.Chrome },
        { "msedge", BrowserType.Edge },
        { "firefox", BrowserType.Firefox },
        { "safari", BrowserType.Safari },
        { "opera", BrowserType.Opera },
        { "iexplore", BrowserType.Internet_Explorer }
    };

    public BrowserTracker(
        ILogger<BrowserTracker> logger,
        IActivityEventRepository repository,
        IOptions<TrackerConfiguration> configuration)
    {
        _logger = logger;
        _repository = repository;
        _configuration = configuration.Value;
        _agentId = Guid.NewGuid();
        _userId = GetCurrentUserId();
        
        _timer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public string Name => "BrowserTracker";
    public bool IsEnabled => _configuration.Enabled;
    public int IntervalSeconds => _configuration.IntervalSeconds;

    public event EventHandler<ActivityEvent> EventCaptured;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("BrowserTracker está desabilitado");
            return;
        }

        _logger.LogInformation("Iniciando BrowserTracker com intervalo de {IntervalSeconds}s", IntervalSeconds);
        
        _isRunning = true;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(IntervalSeconds));
        
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parando BrowserTracker");
        
        _isRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var events = new List<ActivityEvent>();
        
        try
        {
            var foregroundWindow = WindowsApi.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return events;
            }

            WindowsApi.GetWindowThreadProcessId(foregroundWindow, out uint processId);
            var processName = WindowsApi.GetProcessName(processId).ToLowerInvariant();
            
            // Verifica se é um navegador conhecido
            var browserType = GetBrowserType(processName);
            if (browserType == BrowserType.Other)
            {
                return events;
            }

            var windowTitle = WindowsApi.GetWindowTitle(foregroundWindow);
            var url = ExtractUrlFromTitle(windowTitle, browserType);
            var pageTitle = ExtractPageTitleFromWindowTitle(windowTitle, browserType);

            // Verifica se houve mudança na URL
            if (url == _lastUrl && browserType == _lastBrowserType)
            {
                return events;
            }

            var currentTime = DateTime.UtcNow;
            var domain = ExtractDomainFromUrl(url);
            
            // Calcula duração da página anterior
            var duration = _lastCaptureTime != DateTime.MinValue ? 
                          currentTime - _lastCaptureTime : 
                          TimeSpan.Zero;

            // Cria evento de atividade
            var activityEvent = new ActivityEvent
            {
                Id = Guid.NewGuid(),
                AgentId = _agentId,
                UserId = _userId,
                Timestamp = currentTime,
                Type = ActivityType.BrowserNavigation,
                Application = GetBrowserDisplayName(browserType),
                WindowTitle = windowTitle,
                Url = url,
                Duration = duration,
                IsActive = true,
                CreatedAt = currentTime,
                UpdatedAt = currentTime
            };

            // Cria evento específico de navegador
            var browserEvent = new BrowserEvent
            {
                Id = Guid.NewGuid(),
                ActivityEventId = activityEvent.Id,
                BrowserType = browserType,
                Url = url,
                Domain = domain,
                PageTitle = pageTitle,
                NavigationType = GetNavigationType(url, _lastUrl),
                PreviousUrl = _lastUrl,
                TimeOnPage = (int)duration.TotalSeconds,
                TabCount = EstimateTabCount(windowTitle, browserType),
                IsActiveTab = true,
                IsIncognito = IsIncognitoMode(windowTitle, browserType),
                Timestamp = currentTime
            };

            // Adiciona metadados
            var metadata = new Dictionary<string, object>
            {
                { "BrowserType", browserType.ToString() },
                { "Domain", domain },
                { "NavigationType", browserEvent.NavigationType.ToString() },
                { "IsIncognito", browserEvent.IsIncognito },
                { "TabCount", browserEvent.TabCount }
            };

            activityEvent.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);

            // Salva no banco de dados
            await _repository.AddActivityEventAsync(activityEvent, cancellationToken);
            await _repository.AddBrowserEventAsync(browserEvent, cancellationToken);

            events.Add(activityEvent);

            // Atualiza estado interno
            _lastUrl = url;
            _lastPageTitle = pageTitle;
            _lastBrowserType = browserType;
            _lastCaptureTime = currentTime;

            // Dispara evento
            EventCaptured?.Invoke(this, activityEvent);

            _logger.LogDebug("Capturado evento de navegador: {BrowserType} - {Url}", 
                browserType, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar evento de navegador");
        }

        return events;
    }

    private BrowserType GetBrowserType(string processName)
    {
        foreach (var kvp in _browserProcesses)
        {
            if (processName.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }
        return BrowserType.Other;
    }

    private string GetBrowserDisplayName(BrowserType browserType)
    {
        return browserType switch
        {
            BrowserType.Chrome => "Google Chrome",
            BrowserType.Edge => "Microsoft Edge",
            BrowserType.Firefox => "Mozilla Firefox",
            BrowserType.Safari => "Safari",
            BrowserType.Opera => "Opera",
            BrowserType.Internet_Explorer => "Internet Explorer",
            _ => "Unknown Browser"
        };
    }

    private string ExtractUrlFromTitle(string windowTitle, BrowserType browserType)
    {
        try
        {
            // Padrões específicos para cada navegador
            var patterns = browserType switch
            {
                BrowserType.Chrome => new[] { @"https?://[^\s]+", @"[^\s]+ - Google Chrome" },
                BrowserType.Edge => new[] { @"https?://[^\s]+", @"[^\s]+ - Microsoft Edge" },
                BrowserType.Firefox => new[] { @"https?://[^\s]+", @"[^\s]+ - Mozilla Firefox" },
                _ => new[] { @"https?://[^\s]+" }
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(windowTitle, pattern);
                if (match.Success && match.Value.StartsWith("http"))
                {
                    return match.Value;
                }
            }

            // Fallback: tentar extrair URL da barra de endereços (método mais complexo)
            return ExtractUrlFromAddressBar(windowTitle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao extrair URL do título: {WindowTitle}", windowTitle);
            return string.Empty;
        }
    }

    private string ExtractUrlFromAddressBar(string windowTitle)
    {
        // Implementação simplificada - em produção seria mais complexa
        // usando UI Automation para acessar a barra de endereços
        var urlMatch = Regex.Match(windowTitle, @"https?://[^\s]+");
        return urlMatch.Success ? urlMatch.Value : string.Empty;
    }

    private string ExtractPageTitleFromWindowTitle(string windowTitle, BrowserType browserType)
    {
        try
        {
            var browserName = GetBrowserDisplayName(browserType);
            var parts = windowTitle.Split(new[] { $" - {browserName}" }, StringSplitOptions.None);
            return parts.Length > 0 ? parts[0].Trim() : windowTitle;
        }
        catch
        {
            return windowTitle;
        }
    }

    private string ExtractDomainFromUrl(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return string.Empty;
            }
            return uri.Host;
        }
        catch
        {
            return string.Empty;
        }
    }

    private NavigationType GetNavigationType(string currentUrl, string previousUrl)
    {
        if (string.IsNullOrEmpty(previousUrl))
            return NavigationType.PageLoad;

        if (currentUrl == previousUrl)
            return NavigationType.Refresh;

        return NavigationType.Navigation;
    }

    private int EstimateTabCount(string windowTitle, BrowserType browserType)
    {
        // Implementação simplificada - em produção seria mais precisa
        // usando UI Automation para contar abas
        return 1;
    }

    private bool IsIncognitoMode(string windowTitle, BrowserType browserType)
    {
        var incognitoIndicators = new[]
        {
            "InPrivate",
            "Incognito",
            "Private",
            "Privado"
        };

        return incognitoIndicators.Any(indicator => 
            windowTitle.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private async void OnTimerElapsed(object? state)
    {
        if (!_isRunning) return;

        try
        {
            await CaptureAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no timer do BrowserTracker");
        }
    }

    private Guid GetCurrentUserId()
    {
        try
        {
            var userName = Environment.UserName;
            var domainName = Environment.UserDomainName;
            var userIdentifier = $"{domainName}\\{userName}";
            
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userIdentifier));
            return new Guid(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ID do usuário");
            return Guid.NewGuid();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _timer?.Dispose();
                StopAsync().Wait();
            }
            _disposed = true;
        }
    }
}