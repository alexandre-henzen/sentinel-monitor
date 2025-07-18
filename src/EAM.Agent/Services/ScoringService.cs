using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EAM.Agent.Services;

public class ScoringService
{
    private readonly ILogger<ScoringService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, int> _applicationScores;
    private readonly Dictionary<string, string[]> _categories;

    public ScoringService(ILogger<ScoringService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _applicationScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _categories = new Dictionary<string, string[]>();
        
        LoadScoringRules();
    }

    public Task<int> CalculateProductivityScoreAsync(string? processName, string? windowTitle = null)
    {
        if (string.IsNullOrEmpty(processName))
            return Task.FromResult(GetDefaultScore());

        var score = CalculateApplicationScore(processName);
        
        // Ajustar score baseado no título da janela se disponível
        if (!string.IsNullOrEmpty(windowTitle))
        {
            score = AdjustScoreByWindowTitle(score, windowTitle, processName);
        }

        _logger.LogDebug("Score calculado para {ProcessName}: {Score}", processName, score);
        return Task.FromResult(score);
    }

    public Task<int> CalculateUrlProductivityScoreAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
            return Task.FromResult(GetDefaultScore());

        try
        {
            var uri = new Uri(url);
            var domain = uri.Host.ToLowerInvariant();
            
            // Remover www. se presente
            if (domain.StartsWith("www."))
                domain = domain.Substring(4);

            var score = CalculateDomainScore(domain);
            
            _logger.LogDebug("Score calculado para URL {Url}: {Score}", url, score);
            return Task.FromResult(score);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro calculando score para URL {Url}", url);
            return Task.FromResult(GetDefaultScore());
        }
    }

    public Task<string> GetProductivityCategoryAsync(string? processName)
    {
        if (string.IsNullOrEmpty(processName))
            return Task.FromResult("Neutral");

        var category = DetermineCategory(processName);
        return Task.FromResult(category);
    }

    private void LoadScoringRules()
    {
        try
        {
            // Carregar categorias da configuração
            var configSection = _configuration.GetSection("Scoring:Categories");
            foreach (var category in configSection.GetChildren())
            {
                var applications = category.Get<string[]>();
                if (applications != null)
                {
                    _categories[category.Key] = applications;
                }
            }

            // Converter categorias em scores
            ConvertCategoriesToScores();
            
            _logger.LogInformation("Regras de scoring carregadas: {Count} aplicações", _applicationScores.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro carregando regras de scoring");
        }
    }

    private void ConvertCategoriesToScores()
    {
        // Productive = 80-100
        if (_categories.TryGetValue("Productive", out var productive))
        {
            foreach (var app in productive)
            {
                _applicationScores[app] = 85;
            }
        }

        // Neutral = 40-60
        if (_categories.TryGetValue("Neutral", out var neutral))
        {
            foreach (var app in neutral)
            {
                _applicationScores[app] = 50;
            }
        }

        // Unproductive = 0-30
        if (_categories.TryGetValue("Unproductive", out var unproductive))
        {
            foreach (var app in unproductive)
            {
                _applicationScores[app] = 15;
            }
        }
    }

    private int CalculateApplicationScore(string processName)
    {
        // Buscar score exato
        if (_applicationScores.TryGetValue(processName, out var exactScore))
            return exactScore;

        // Buscar score por substring
        foreach (var kvp in _applicationScores)
        {
            if (processName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains(processName, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // Score baseado em heurísticas
        return CalculateHeuristicScore(processName);
    }

    private int CalculateHeuristicScore(string processName)
    {
        var lowerName = processName.ToLowerInvariant();

        // Desenvolvimento e produtividade
        if (lowerName.Contains("visual") || lowerName.Contains("code") || lowerName.Contains("studio") ||
            lowerName.Contains("git") || lowerName.Contains("sql") || lowerName.Contains("docker"))
            return 85;

        // Browsers (score neutro, depende do conteúdo)
        if (lowerName.Contains("chrome") || lowerName.Contains("firefox") || lowerName.Contains("edge") ||
            lowerName.Contains("safari") || lowerName.Contains("browser"))
            return 50;

        // Jogos
        if (lowerName.Contains("game") || lowerName.Contains("steam") || lowerName.Contains("epic"))
            return 10;

        // Sistema
        if (lowerName.Contains("system") || lowerName.Contains("svchost") || lowerName.Contains("dwm"))
            return 0; // Processos de sistema não pontuam

        return GetDefaultScore();
    }

    private int CalculateDomainScore(string domain)
    {
        // Domínios produtivos
        var productiveDomains = new[]
        {
            "github.com", "stackoverflow.com", "docs.microsoft.com", "developer.mozilla.org",
            "aws.amazon.com", "azure.microsoft.com", "google.com", "gmail.com", "outlook.com"
        };

        // Domínios improdutivos
        var unproductiveDomains = new[]
        {
            "facebook.com", "twitter.com", "instagram.com", "tiktok.com", "youtube.com",
            "netflix.com", "twitch.tv", "reddit.com", "9gag.com"
        };

        if (productiveDomains.Any(d => domain.Contains(d)))
            return 80;

        if (unproductiveDomains.Any(d => domain.Contains(d)))
            return 20;

        // Domínios de trabalho (heurística)
        if (domain.Contains("corp") || domain.Contains("company") || domain.Contains("work") ||
            domain.Contains("office") || domain.Contains("intranet"))
            return 85;

        return GetDefaultScore();
    }

    private int AdjustScoreByWindowTitle(int baseScore, string windowTitle, string processName)
    {
        var lowerTitle = windowTitle.ToLowerInvariant();

        // Ajustes específicos por aplicação
        if (processName.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
            processName.Contains("edge", StringComparison.OrdinalIgnoreCase))
        {
            // Para browsers, usar o título para determinar produtividade
            if (lowerTitle.Contains("youtube") || lowerTitle.Contains("facebook") || 
                lowerTitle.Contains("instagram") || lowerTitle.Contains("twitter"))
                return 20;

            if (lowerTitle.Contains("github") || lowerTitle.Contains("stackoverflow") ||
                lowerTitle.Contains("docs") || lowerTitle.Contains("documentation"))
                return 80;
        }

        // Reuniões e comunicação
        if (lowerTitle.Contains("meeting") || lowerTitle.Contains("call") || lowerTitle.Contains("teams"))
            return Math.Max(baseScore, 70);

        return baseScore;
    }

    private string DetermineCategory(string processName)
    {
        foreach (var category in _categories)
        {
            if (category.Value.Any(app => 
                string.Equals(app, processName, StringComparison.OrdinalIgnoreCase) ||
                processName.Contains(app, StringComparison.OrdinalIgnoreCase)))
            {
                return category.Key;
            }
        }

        return "Neutral";
    }

    private int GetDefaultScore()
    {
        return _configuration.GetValue<int>("Scoring:DefaultProductivityScore", 50);
    }
}