using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EAM.Agent.Core.Models;
using EAM.Agent.Core.Data;
using ActivityEvent = EAM.Agent.Core.Models.ActivityEvent;

namespace EAM.Agent.Core.Services;

/// <summary>
/// Engine para pontuação de produtividade baseada em atividades
/// </summary>
public class ScoringEngine : IDisposable
{
    private readonly ILogger<ScoringEngine> _logger;
    private readonly IActivityEventRepository _repository;
    private readonly ScoringConfiguration _configuration;
    private readonly Dictionary<string, ProductivityCategory> _applicationCategories;
    private readonly Dictionary<string, ProductivityCategory> _domainCategories;
    private readonly Dictionary<string, int> _customScores;
    private bool _disposed = false;

    public ScoringEngine(
        ILogger<ScoringEngine> logger,
        IActivityEventRepository repository,
        IOptions<ScoringConfiguration> configuration)
    {
        _logger = logger;
        _repository = repository;
        _configuration = configuration.Value;
        _applicationCategories = new Dictionary<string, ProductivityCategory>();
        _domainCategories = new Dictionary<string, ProductivityCategory>();
        _customScores = new Dictionary<string, int>();
        
        InitializeDefaultCategories();
        LoadCustomCategories();
    }

    /// <summary>
    /// Aplica pontuação de produtividade a um evento de atividade
    /// </summary>
    public async Task<int> ScoreActivityEventAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var score = CalculateProductivityScore(activityEvent);
            
            // Atualiza o evento com a pontuação
            activityEvent.ProductivityScore = score;
            activityEvent.UpdatedAt = DateTime.UtcNow;
            
            await _repository.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug("Pontuação aplicada: {Application} - {Score}", 
                activityEvent.Application, score);
            
            return score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aplicar pontuação de produtividade");
            return _configuration.DefaultProductivityScore;
        }
    }

    /// <summary>
    /// Pontua múltiplos eventos de atividade
    /// </summary>
    public async Task<Dictionary<Guid, int>> ScoreMultipleEventsAsync(
        IEnumerable<ActivityEvent> events, 
        CancellationToken cancellationToken = default)
    {
        var scores = new Dictionary<Guid, int>();
        
        try
        {
            foreach (var eventItem in events)
            {
                var score = await ScoreActivityEventAsync(eventItem, cancellationToken);
                scores[eventItem.Id] = score;
            }
            
            _logger.LogDebug("Pontuados {Count} eventos", scores.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pontuar múltiplos eventos");
        }
        
        return scores;
    }

    /// <summary>
    /// Calcula pontuação de produtividade para um evento específico
    /// </summary>
    private int CalculateProductivityScore(ActivityEvent activityEvent)
    {
        try
        {
            // Verifica se há pontuação customizada específica
            var customScore = GetCustomScore(activityEvent);
            if (customScore.HasValue)
            {
                return customScore.Value;
            }
            
            // Pontuação baseada no tipo de evento
            var baseScore = GetBaseScoreByEventType(activityEvent.Type);
            
            // Pontuação baseada na aplicação
            var applicationScore = GetApplicationScore(activityEvent.Application);
            
            // Pontuação baseada na URL/domínio
            var domainScore = GetDomainScore(activityEvent.Url);
            
            // Pontuação baseada no tempo (horário de trabalho)
            var timeScore = GetTimeBasedScore(activityEvent.Timestamp);
            
            // Pontuação baseada na duração
            var durationScore = GetDurationScore(activityEvent.Duration);
            
            // Calcula pontuação final (média ponderada)
            var finalScore = CalculateWeightedScore(
                baseScore, 
                applicationScore, 
                domainScore, 
                timeScore, 
                durationScore);
            
            // Garante que a pontuação está no intervalo válido
            return Math.Clamp(finalScore, 0, 100);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao calcular pontuação, usando padrão");
            return _configuration.DefaultProductivityScore;
        }
    }

    private int? GetCustomScore(ActivityEvent activityEvent)
    {
        // Verifica pontuação customizada por aplicação específica
        if (!string.IsNullOrEmpty(activityEvent.Application))
        {
            var appKey = activityEvent.Application.ToLowerInvariant();
            if (_customScores.ContainsKey(appKey))
            {
                return _customScores[appKey];
            }
        }
        
        // Verifica pontuação customizada por URL/domínio
        if (!string.IsNullOrEmpty(activityEvent.Url))
        {
            var domain = ExtractDomainFromUrl(activityEvent.Url);
            if (!string.IsNullOrEmpty(domain) && _customScores.ContainsKey(domain))
            {
                return _customScores[domain];
            }
        }
        
        return null;
    }

    private int GetBaseScoreByEventType(ActivityType eventType)
    {
        return eventType switch
        {
            ActivityType.WindowFocus => 50,
            ActivityType.ApplicationStart => 60,
            ActivityType.ApplicationEnd => 50,
            ActivityType.BrowserNavigation => 40,
            ActivityType.TeamsCall => 80,
            ActivityType.TeamsCallEnd => 80,
            ActivityType.Screenshot => 50,
            ActivityType.ProcessStart => 60,
            ActivityType.ProcessEnd => 50,
            ActivityType.SystemIdle => 0,
            ActivityType.SystemActive => 70,
            _ => _configuration.DefaultProductivityScore
        };
    }

    private int GetApplicationScore(string? application)
    {
        if (string.IsNullOrEmpty(application))
            return _configuration.DefaultProductivityScore;
        
        var appKey = application.ToLowerInvariant();
        
        // Verifica se a aplicação tem categoria definida
        if (_applicationCategories.ContainsKey(appKey))
        {
            return GetScoreByCategory(_applicationCategories[appKey]);
        }
        
        // Verifica aplicações por padrão
        if (IsProductiveApplication(appKey))
            return _configuration.ProductiveScore;
        
        if (IsUnproductiveApplication(appKey))
            return _configuration.UnproductiveScore;
        
        return _configuration.NeutralScore;
    }

    private int GetDomainScore(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return _configuration.DefaultProductivityScore;
        
        var domain = ExtractDomainFromUrl(url);
        if (string.IsNullOrEmpty(domain))
            return _configuration.DefaultProductivityScore;
        
        var domainKey = domain.ToLowerInvariant();
        
        // Verifica se o domínio tem categoria definida
        if (_domainCategories.ContainsKey(domainKey))
        {
            return GetScoreByCategory(_domainCategories[domainKey]);
        }
        
        // Verifica domínios por padrão
        if (IsProductiveDomain(domainKey))
            return _configuration.ProductiveScore;
        
        if (IsUnproductiveDomain(domainKey))
            return _configuration.UnproductiveScore;
        
        return _configuration.NeutralScore;
    }

    private int GetTimeBasedScore(DateTime timestamp)
    {
        // Pontuação baseada no horário (horário comercial = mais produtivo)
        var hour = timestamp.Hour;
        
        if (hour >= 9 && hour <= 17) // Horário comercial
            return 100;
        
        if (hour >= 8 && hour <= 18) // Horário estendido
            return 80;
        
        if (hour >= 7 && hour <= 22) // Horário normal
            return 60;
        
        return 30; // Horário noturno/madrugada
    }

    private int GetDurationScore(TimeSpan duration)
    {
        // Pontuação baseada na duração da atividade
        var minutes = duration.TotalMinutes;
        
        if (minutes < 1) // Muito curta
            return 20;
        
        if (minutes < 5) // Curta
            return 50;
        
        if (minutes < 30) // Média
            return 80;
        
        if (minutes < 120) // Longa
            return 100;
        
        return 70; // Muito longa (pode indicar distração)
    }

    private int CalculateWeightedScore(
        int baseScore, 
        int applicationScore, 
        int domainScore, 
        int timeScore, 
        int durationScore)
    {
        // Pesos para cada componente da pontuação
        var weights = _configuration.ScoringWeights;
        
        var weightedScore = 
            (baseScore * weights.EventType) +
            (applicationScore * weights.Application) +
            (domainScore * weights.Domain) +
            (timeScore * weights.TimeOfDay) +
            (durationScore * weights.Duration);
        
        var totalWeight = weights.EventType + weights.Application + weights.Domain + 
                         weights.TimeOfDay + weights.Duration;
        
        return (int)(weightedScore / totalWeight);
    }

    private int GetScoreByCategory(ProductivityCategory category)
    {
        return category switch
        {
            ProductivityCategory.Productive => _configuration.ProductiveScore,
            ProductivityCategory.Neutral => _configuration.NeutralScore,
            ProductivityCategory.Unproductive => _configuration.UnproductiveScore,
            _ => _configuration.DefaultProductivityScore
        };
    }

    private bool IsProductiveApplication(string application)
    {
        var productiveApps = new[]
        {
            "code", "devenv", "rider", "intellij", "eclipse", "atom", "sublime",
            "winword", "excel", "powerpnt", "outlook", "onenote", "teams",
            "slack", "discord", "zoom", "skype", "webex",
            "notepad", "notepad++", "vim", "emacs",
            "chrome", "firefox", "edge", "safari" // Contexto dependente
        };
        
        return productiveApps.Any(app => application.Contains(app, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsUnproductiveApplication(string application)
    {
        var unproductiveApps = new[]
        {
            "game", "steam", "origin", "uplay", "battle.net", "epic",
            "netflix", "youtube", "spotify", "vlc", "mediaplayer",
            "solitaire", "minesweeper", "candy", "angry"
        };
        
        return unproductiveApps.Any(app => application.Contains(app, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsProductiveDomain(string domain)
    {
        var productiveDomains = new[]
        {
            "github.com", "stackoverflow.com", "docs.microsoft.com", "developer.mozilla.org",
            "linkedin.com", "office.com", "office365.com", "teams.microsoft.com",
            "google.com", "bing.com", "wikipedia.org", "w3schools.com",
            "stackoverflow.com", "stackexchange.com", "medium.com", "dev.to"
        };
        
        return productiveDomains.Any(d => domain.Contains(d, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsUnproductiveDomain(string domain)
    {
        var unproductiveDomains = new[]
        {
            "facebook.com", "instagram.com", "twitter.com", "tiktok.com",
            "youtube.com", "netflix.com", "twitch.tv", "reddit.com",
            "9gag.com", "buzzfeed.com", "dailymail.co.uk", "thesun.co.uk",
            "game", "casino", "bet", "gambling"
        };
        
        return unproductiveDomains.Any(d => domain.Contains(d, StringComparison.OrdinalIgnoreCase));
    }

    private string ExtractDomainFromUrl(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }
        catch
        {
            // Ignora erro
        }
        
        return string.Empty;
    }

    private void InitializeDefaultCategories()
    {
        // Aplicações produtivas
        var productiveApps = new[]
        {
            "code", "devenv", "rider", "winword", "excel", "powerpnt", "outlook", "teams"
        };
        
        foreach (var app in productiveApps)
        {
            _applicationCategories[app] = ProductivityCategory.Productive;
        }
        
        // Aplicações não produtivas
        var unproductiveApps = new[]
        {
            "steam", "game", "netflix", "youtube", "spotify"
        };
        
        foreach (var app in unproductiveApps)
        {
            _applicationCategories[app] = ProductivityCategory.Unproductive;
        }
        
        // Domínios produtivos
        var productiveDomains = new[]
        {
            "github.com", "stackoverflow.com", "docs.microsoft.com", "office.com"
        };
        
        foreach (var domain in productiveDomains)
        {
            _domainCategories[domain] = ProductivityCategory.Productive;
        }
        
        // Domínios não produtivos
        var unproductiveDomains = new[]
        {
            "facebook.com", "instagram.com", "twitter.com", "youtube.com"
        };
        
        foreach (var domain in unproductiveDomains)
        {
            _domainCategories[domain] = ProductivityCategory.Unproductive;
        }
    }

    private void LoadCustomCategories()
    {
        try
        {
            // Carrega configurações personalizadas
            if (_configuration.CustomCategories != null)
            {
                foreach (var category in _configuration.CustomCategories)
                {
                    _applicationCategories[category.Key.ToLowerInvariant()] = category.Value;
                }
            }
            
            if (_configuration.CustomScores != null)
            {
                foreach (var score in _configuration.CustomScores)
                {
                    _customScores[score.Key.ToLowerInvariant()] = score.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao carregar categorias customizadas");
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
                // Cleanup resources
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Categorias de produtividade
/// </summary>
public enum ProductivityCategory
{
    Productive = 1,
    Neutral = 2,
    Unproductive = 3
}

/// <summary>
/// Configuração do sistema de pontuação
/// </summary>
public class ScoringConfiguration
{
    public int DefaultProductivityScore { get; set; } = 50;
    public int ProductiveScore { get; set; } = 80;
    public int NeutralScore { get; set; } = 60;
    public int UnproductiveScore { get; set; } = 30;
    public ScoringWeights ScoringWeights { get; set; } = new();
    public Dictionary<string, ProductivityCategory> CustomCategories { get; set; } = new();
    public Dictionary<string, int> CustomScores { get; set; } = new();
}

/// <summary>
/// Pesos para componentes da pontuação
/// </summary>
public class ScoringWeights
{
    public double EventType { get; set; } = 0.2;
    public double Application { get; set; } = 0.3;
    public double Domain { get; set; } = 0.2;
    public double TimeOfDay { get; set; } = 0.15;
    public double Duration { get; set; } = 0.15;
}