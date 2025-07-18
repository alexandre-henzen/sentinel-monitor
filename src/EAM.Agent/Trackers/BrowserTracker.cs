using EAM.PluginSDK;
using EAM.Agent.Helpers;
using EAM.Agent.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace EAM.Agent.Trackers;

public class BrowserTracker : ITracker
{
    private readonly ILogger<BrowserTracker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ScoringService _scoringService;
    
    private readonly string[] _supportedBrowsers = { "chrome", "firefox", "edge", "opera", "brave", "safari" };
    private readonly Dictionary<string, string> _lastUrls = new();
    private readonly Dictionary<string, string> _lastTitles = new();

    public string Name => "BrowserTracker";
    public bool IsEnabled => _configuration.GetValue<bool>("Trackers:BrowserTracker:Enabled");

    public BrowserTracker(ILogger<BrowserTracker> logger, IConfiguration configuration, ScoringService scoringService)
    {
        _logger = logger;
        _configuration = configuration;
        _scoringService = scoringService;
    }

    public Task InitializeAsync()
    {
        _logger.LogInformation("BrowserTracker inicializado");
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<ActivityEvent>> CaptureAsync()
    {
        var events = new List<ActivityEvent>();

        try
        {
            foreach (var browserName in _supportedBrowsers)
            {
                var browserEvents = await CaptureBrowserEventsAsync(browserName);
                events.AddRange(browserEvents);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro capturando eventos de browser");
        }

        return events;
    }

    public Task StopAsync()
    {
        _logger.LogInformation("BrowserTracker parado");
        return Task.CompletedTask;
    }

    private async Task<List<ActivityEvent>> CaptureBrowserEventsAsync(string browserName)
    {
        var events = new List<ActivityEvent>();

        try
        {
            var browserWindows = Win32Helper.GetWindowsByProcessName(browserName);
            
            foreach (var windowHandle in browserWindows)
            {
                var browserInfo = await GetBrowserInfoAsync(windowHandle, browserName);
                if (browserInfo == null) continue;

                var key = $"{browserName}_{windowHandle}";
                var hasUrlChanged = !_lastUrls.TryGetValue(key, out var lastUrl) || lastUrl != browserInfo.Url;
                var hasTitleChanged = !_lastTitles.TryGetValue(key, out var lastTitle) || lastTitle != browserInfo.Title;

                if (hasUrlChanged || hasTitleChanged)
                {
                    var productivityScore = await _scoringService.CalculateUrlProductivityScoreAsync(browserInfo.Url);

                    var urlEvent = new ActivityEvent("BrowserUrl")
                    {
                        ApplicationName = browserName,
                        WindowTitle = browserInfo.Title,
                        Url = browserInfo.Url,
                        ProcessName = browserName,
                        ProcessId = browserInfo.ProcessId,
                        ProductivityScore = productivityScore,
                        Timestamp = DateTime.UtcNow
                    };

                    // Adicionar metadados
                    urlEvent.AddMetadata("browser_name", browserName);
                    urlEvent.AddMetadata("window_handle", windowHandle.ToString());
                    urlEvent.AddMetadata("domain", ExtractDomain(browserInfo.Url));
                    urlEvent.AddMetadata("url_changed", hasUrlChanged);
                    urlEvent.AddMetadata("title_changed", hasTitleChanged);

                    events.Add(urlEvent);

                    // Atualizar cache
                    _lastUrls[key] = browserInfo.Url;
                    _lastTitles[key] = browserInfo.Title;

                    _logger.LogDebug("URL capturada: {Browser} - {Url}", browserName, browserInfo.Url);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro capturando eventos do browser {Browser}", browserName);
        }

        return events;
    }

    private async Task<BrowserInfo?> GetBrowserInfoAsync(IntPtr windowHandle, string browserName)
    {
        try
        {
            var title = Win32Helper.GetWindowText(windowHandle);
            var processId = Win32Helper.GetWindowProcessId(windowHandle);
            
            var url = await ExtractUrlFromBrowserAsync(windowHandle, browserName, title);
            
            return new BrowserInfo
            {
                Title = title,
                Url = url,
                ProcessId = (int)processId,
                WindowHandle = windowHandle
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro obtendo informações do browser {Browser}", browserName);
            return null;
        }
    }

    private async Task<string> ExtractUrlFromBrowserAsync(IntPtr windowHandle, string browserName, string title)
    {
        try
        {
            // Estratégia 1: Extrair URL do título da janela
            var urlFromTitle = ExtractUrlFromTitle(title);
            if (!string.IsNullOrEmpty(urlFromTitle))
                return urlFromTitle;

            // Estratégia 2: Usar automação específica do browser
            var urlFromAutomation = await ExtractUrlUsingAutomationAsync(windowHandle, browserName);
            if (!string.IsNullOrEmpty(urlFromAutomation))
                return urlFromAutomation;

            // Estratégia 3: Usar heurísticas no título
            var urlFromHeuristics = ExtractUrlFromHeuristics(title);
            if (!string.IsNullOrEmpty(urlFromHeuristics))
                return urlFromHeuristics;

            return $"unknown://{title}";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro extraindo URL do browser {Browser}", browserName);
            return $"error://{title}";
        }
    }

    private string ExtractUrlFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;

        // Padrões comuns de URLs em títulos
        var urlPatterns = new[]
        {
            @"https?://[^\s]+",
            @"www\.[^\s]+",
            @"[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}/[^\s]*"
        };

        foreach (var pattern in urlPatterns)
        {
            var match = Regex.Match(title, pattern);
            if (match.Success)
            {
                var url = match.Value.Trim();
                if (!url.StartsWith("http"))
                    url = "https://" + url;
                return url;
            }
        }

        return string.Empty;
    }

    private async Task<string> ExtractUrlUsingAutomationAsync(IntPtr windowHandle, string browserName)
    {
        try
        {
            // Para Chrome e Edge (Chromium-based)
            if (browserName.Contains("chrome") || browserName.Contains("edge"))
            {
                return await ExtractUrlFromChromiumAsync(windowHandle);
            }

            // Para Firefox
            if (browserName.Contains("firefox"))
            {
                return await ExtractUrlFromFirefoxAsync(windowHandle);
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro usando automação para extrair URL do {Browser}", browserName);
            return string.Empty;
        }
    }

    private async Task<string> ExtractUrlFromChromiumAsync(IntPtr windowHandle)
    {
        try
        {
            // Procurar pela barra de endereços (address bar)
            var addressBarHandle = Win32Helper.FindWindowEx(windowHandle, IntPtr.Zero, "Chrome_WidgetWin_1", null);
            if (addressBarHandle != IntPtr.Zero)
            {
                var addressBar = Win32Helper.FindWindowEx(addressBarHandle, IntPtr.Zero, "Chrome_RenderWidgetHostHWND", null);
                if (addressBar != IntPtr.Zero)
                {
                    // Tentar obter o texto da barra de endereços
                    var url = Win32Helper.GetWindowText(addressBar);
                    if (!string.IsNullOrEmpty(url) && (url.StartsWith("http") || url.Contains(".")))
                    {
                        return url;
                    }
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro extraindo URL do Chromium");
            return string.Empty;
        }
    }

    private async Task<string> ExtractUrlFromFirefoxAsync(IntPtr windowHandle)
    {
        try
        {
            // Firefox usa uma estrutura diferente
            var firefoxWindow = Win32Helper.FindWindowEx(windowHandle, IntPtr.Zero, "MozillaWindowClass", null);
            if (firefoxWindow != IntPtr.Zero)
            {
                // Implementar lógica específica do Firefox se necessário
                return string.Empty;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro extraindo URL do Firefox");
            return string.Empty;
        }
    }

    private string ExtractUrlFromHeuristics(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;

        // Remover sufixos comuns de browsers
        var cleanTitle = title
            .Replace(" - Google Chrome", "")
            .Replace(" - Mozilla Firefox", "")
            .Replace(" - Microsoft Edge", "")
            .Replace(" - Opera", "")
            .Trim();

        // Verificar se o título contém domínios conhecidos
        var commonDomains = new[]
        {
            "google.com", "youtube.com", "github.com", "stackoverflow.com",
            "microsoft.com", "amazon.com", "facebook.com", "twitter.com",
            "linkedin.com", "instagram.com", "reddit.com", "wikipedia.org"
        };

        foreach (var domain in commonDomains)
        {
            if (cleanTitle.Contains(domain, StringComparison.OrdinalIgnoreCase))
            {
                return $"https://{domain}";
            }
        }

        return string.Empty;
    }

    private string ExtractDomain(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;

        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return string.Empty;
        }
    }

    private class BrowserInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public IntPtr WindowHandle { get; set; }
    }
}