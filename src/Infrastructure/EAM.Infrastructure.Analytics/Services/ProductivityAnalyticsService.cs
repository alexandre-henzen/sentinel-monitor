using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Data;
using EAM.Infrastructure.Analytics.Models;

namespace EAM.Infrastructure.Analytics.Services;

/// <summary>
/// Serviço para cálculo de métricas de produtividade
/// </summary>
public class ProductivityAnalyticsService : IProductivityAnalyticsService
{
    private readonly EamDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ProductivityAnalyticsService> _logger;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="context">Context do banco de dados</param>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="logger">Logger</param>
    public ProductivityAnalyticsService(
        EamDbContext context,
        ICacheService cacheService,
        ILogger<ProductivityAnalyticsService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Calcula métricas de produtividade para um agente em uma data específica
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="date">Data para cálculo</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Métricas de produtividade</returns>
    public async Task<ProductivityMetrics> CalculateDailyMetricsAsync(
        Guid agentId, 
        DateTime date, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Calculando métricas diárias para agente {AgentId} na data {Date}", 
                agentId, date.ToString("yyyy-MM-dd"));

            var startDate = date.Date;
            var endDate = startDate.AddDays(1);

            // Buscar eventos do dia
            var events = await _context.ActivityEvents
                .Where(e => e.AgentId == agentId && 
                           e.Timestamp >= startDate && 
                           e.Timestamp < endDate)
                .OrderBy(e => e.Timestamp)
                .ToListAsync(cancellationToken);

            if (!events.Any())
            {
                _logger.LogDebug("Nenhum evento encontrado para agente {AgentId} na data {Date}", 
                    agentId, date.ToString("yyyy-MM-dd"));
                
                return new ProductivityMetrics
                {
                    AgentId = agentId,
                    Date = date,
                    LastUpdated = DateTime.UtcNow
                };
            }

            var metrics = new ProductivityMetrics
            {
                AgentId = agentId,
                Date = date,
                LastUpdated = DateTime.UtcNow
            };

            // Calcular métricas básicas
            await CalculateBasicMetrics(metrics, events, cancellationToken);

            // Calcular métricas por aplicação
            await CalculateApplicationMetrics(metrics, events, cancellationToken);

            // Calcular métricas por hora
            await CalculateHourlyMetrics(metrics, events, cancellationToken);

            // Calcular scores de produtividade
            await CalculateProductivityScores(metrics, cancellationToken);

            // Cache das métricas
            var cacheKey = $"metrics:daily:{agentId}:{date:yyyy-MM-dd}";
            await _cacheService.SetAsync(cacheKey, metrics, TimeSpan.FromHours(4), cancellationToken);

            _logger.LogInformation("Métricas calculadas para agente {AgentId} na data {Date}: Score {Score}",
                agentId, date.ToString("yyyy-MM-dd"), metrics.ProductivityScore);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular métricas para agente {AgentId} na data {Date}", 
                agentId, date.ToString("yyyy-MM-dd"));
            throw;
        }
    }

    /// <summary>
    /// Calcula métricas agregadas para um período
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="periodType">Tipo de período</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Métricas agregadas</returns>
    public async Task<AggregatedMetrics> CalculateAggregatedMetricsAsync(
        Guid agentId, 
        DateTime startDate, 
        DateTime endDate, 
        PeriodType periodType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Calculando métricas agregadas para agente {AgentId} de {StartDate} a {EndDate}",
                agentId, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

            var aggregated = new AggregatedMetrics
            {
                AgentId = agentId,
                StartDate = startDate,
                EndDate = endDate,
                PeriodType = periodType
            };

            // Buscar métricas diárias existentes
            var dailyMetricsList = new List<ProductivityMetrics>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var dailyMetrics = await GetOrCalculateDailyMetricsAsync(agentId, currentDate, cancellationToken);
                if (dailyMetrics.TotalActiveTimeMinutes > 0)
                {
                    dailyMetricsList.Add(dailyMetrics);
                }
                currentDate = currentDate.AddDays(1);
            }

            if (!dailyMetricsList.Any())
            {
                _logger.LogDebug("Nenhuma métrica diária encontrada para o período");
                return aggregated;
            }

            // Agregar métricas
            aggregated.WorkingDays = dailyMetricsList.Count;
            aggregated.TotalActiveTimeMinutes = dailyMetricsList.Sum(m => m.TotalActiveTimeMinutes);
            aggregated.TotalIdleTimeMinutes = dailyMetricsList.Sum(m => m.TotalIdleTimeMinutes);
            aggregated.AverageProductivityScore = dailyMetricsList.Average(m => m.ProductivityScore);
            aggregated.AverageActivityScore = dailyMetricsList.Average(m => m.ActivityScore);
            aggregated.AverageFocusScore = dailyMetricsList.Average(m => m.FocusScore);

            // Agregar aplicações mais utilizadas
            var allApplications = dailyMetricsList.SelectMany(m => m.ApplicationMetrics).ToList();
            aggregated.TopApplications = allApplications
                .GroupBy(a => a.ApplicationName)
                .Select(g => new ApplicationMetrics
                {
                    ApplicationName = g.Key,
                    TotalTimeMinutes = g.Sum(a => a.TotalTimeMinutes),
                    FocusCount = g.Sum(a => a.FocusCount),
                    KeyboardEvents = g.Sum(a => a.KeyboardEvents),
                    MouseClicks = g.Sum(a => a.MouseClicks),
                    IsProductive = g.First().IsProductive
                })
                .OrderByDescending(a => a.TotalTimeMinutes)
                .Take(10)
                .ToList();

            // Calcular percentuais
            var totalAppTime = aggregated.TopApplications.Sum(a => a.TotalTimeMinutes);
            foreach (var app in aggregated.TopApplications)
            {
                app.TimePercentage = totalAppTime > 0 ? (decimal)app.TotalTimeMinutes / totalAppTime * 100 : 0;
            }

            // Calcular tendência
            aggregated.Trend = await CalculateTrendAsync(agentId, startDate, endDate, dailyMetricsList, cancellationToken);

            // Gerar insights
            aggregated.Insights = GenerateInsights(aggregated, dailyMetricsList);

            _logger.LogInformation("Métricas agregadas calculadas para agente {AgentId}: {WorkingDays} dias, Score médio {Score}",
                agentId, aggregated.WorkingDays, aggregated.AverageProductivityScore);

            return aggregated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular métricas agregadas para agente {AgentId}", agentId);
            throw;
        }
    }

    /// <summary>
    /// Compara métricas entre dois períodos
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="currentStart">Início do período atual</param>
    /// <param name="currentEnd">Fim do período atual</param>
    /// <param name="previousStart">Início do período anterior</param>
    /// <param name="previousEnd">Fim do período anterior</param>
    /// <param name="periodType">Tipo de período</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Comparação de métricas</returns>
    public async Task<MetricsComparison> CompareMetricsAsync(
        Guid agentId, 
        DateTime currentStart, 
        DateTime currentEnd,
        DateTime previousStart, 
        DateTime previousEnd,
        PeriodType periodType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Comparando métricas para agente {AgentId} entre períodos", agentId);

            var currentMetrics = await CalculateAggregatedMetricsAsync(
                agentId, currentStart, currentEnd, periodType, cancellationToken);
            
            var previousMetrics = await CalculateAggregatedMetricsAsync(
                agentId, previousStart, previousEnd, periodType, cancellationToken);

            var comparison = new MetricsComparison
            {
                CurrentPeriod = currentMetrics,
                PreviousPeriod = previousMetrics
            };

            // Calcular variações
            if (previousMetrics.AverageProductivityScore > 0)
            {
                comparison.ProductivityChange = 
                    (currentMetrics.AverageProductivityScore - previousMetrics.AverageProductivityScore) / 
                    previousMetrics.AverageProductivityScore * 100;
            }

            if (previousMetrics.AverageActivityScore > 0)
            {
                comparison.ActivityChange = 
                    (currentMetrics.AverageActivityScore - previousMetrics.AverageActivityScore) / 
                    previousMetrics.AverageActivityScore * 100;
            }

            if (previousMetrics.AverageFocusScore > 0)
            {
                comparison.FocusChange = 
                    (currentMetrics.AverageFocusScore - previousMetrics.AverageFocusScore) / 
                    previousMetrics.AverageFocusScore * 100;
            }

            comparison.ActiveTimeChange = 
                currentMetrics.TotalActiveTimeMinutes - previousMetrics.TotalActiveTimeMinutes;

            // Determinar se houve melhora
            comparison.IsImprovement = 
                comparison.ProductivityChange > 0 || 
                comparison.ActivityChange > 0 || 
                comparison.FocusChange > 0;

            // Gerar resumo das principais mudanças
            comparison.KeyChanges = GenerateKeyChanges(comparison);

            _logger.LogInformation("Comparação de métricas concluída para agente {AgentId}: {IsImprovement}",
                agentId, comparison.IsImprovement ? "Melhorou" : "Piorou ou manteve");

            return comparison;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao comparar métricas para agente {AgentId}", agentId);
            throw;
        }
    }

    /// <summary>
    /// Calcula métricas básicas
    /// </summary>
    private async Task CalculateBasicMetrics(ProductivityMetrics metrics, List<dynamic> events, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder para operações assíncronas
        
        // Implementar lógica de cálculo básico
        // Por simplicidade, usando valores placeholder
        metrics.TotalActiveTimeMinutes = 480; // 8 horas
        metrics.TotalIdleTimeMinutes = 60; // 1 hora
        metrics.TotalKeyboardEvents = 1500;
        metrics.TotalMouseClicks = 800;
        metrics.TotalMouseMovements = 2000;
        metrics.TotalApplicationSwitches = 50;
        metrics.TotalScreenshots = 24;
        metrics.StartTime = TimeSpan.FromHours(9);
        metrics.EndTime = TimeSpan.FromHours(18);
        metrics.BreakCount = 3;
        metrics.TotalBreakTimeMinutes = 45;
    }

    /// <summary>
    /// Calcula métricas por aplicação
    /// </summary>
    private async Task CalculateApplicationMetrics(ProductivityMetrics metrics, List<dynamic> events, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder
        
        // Implementar lógica de cálculo por aplicação
        metrics.ApplicationMetrics.Add(new ApplicationMetrics
        {
            ApplicationName = "Visual Studio Code",
            TotalTimeMinutes = 240,
            FocusCount = 15,
            KeyboardEvents = 800,
            MouseClicks = 200,
            TimePercentage = 50,
            IsProductive = true,
            Category = "Development"
        });

        metrics.TopApplication = metrics.ApplicationMetrics.OrderByDescending(a => a.TotalTimeMinutes).First().ApplicationName;
        metrics.TopApplicationTimeMinutes = metrics.ApplicationMetrics.Max(a => a.TotalTimeMinutes);
    }

    /// <summary>
    /// Calcula métricas por hora
    /// </summary>
    private async Task CalculateHourlyMetrics(ProductivityMetrics metrics, List<dynamic> events, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder
        
        // Implementar lógica de cálculo por hora
        for (int hour = 9; hour <= 17; hour++)
        {
            metrics.HourlyMetrics.Add(new HourlyMetrics
            {
                Hour = hour,
                ActiveMinutes = 50,
                IdleMinutes = 10,
                KeyboardEvents = 150,
                MouseClicks = 80,
                ProductivityScore = 75
            });
        }
    }

    /// <summary>
    /// Calcula scores de produtividade
    /// </summary>
    private async Task CalculateProductivityScores(ProductivityMetrics metrics, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder
        
        // Implementar algoritmo de cálculo de scores
        metrics.ActivityScore = Math.Min(100, metrics.ActiveTimePercentage);
        metrics.FocusScore = Math.Min(100, 100 - (metrics.TotalApplicationSwitches * 2));
        metrics.ProductivityScore = (metrics.ActivityScore + metrics.FocusScore) / 2;
    }

    /// <summary>
    /// Obtém ou calcula métricas diárias
    /// </summary>
    private async Task<ProductivityMetrics> GetOrCalculateDailyMetricsAsync(Guid agentId, DateTime date, CancellationToken cancellationToken)
    {
        var cacheKey = $"metrics:daily:{agentId}:{date:yyyy-MM-dd}";
        var cached = await _cacheService.GetAsync<ProductivityMetrics>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        return await CalculateDailyMetricsAsync(agentId, date, cancellationToken);
    }

    /// <summary>
    /// Calcula tendência
    /// </summary>
    private async Task<TrendType> CalculateTrendAsync(Guid agentId, DateTime startDate, DateTime endDate, List<ProductivityMetrics> dailyMetrics, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder
        
        if (dailyMetrics.Count < 3)
        {
            return TrendType.Insufficient;
        }

        var firstHalf = dailyMetrics.Take(dailyMetrics.Count / 2).Average(m => m.ProductivityScore);
        var secondHalf = dailyMetrics.Skip(dailyMetrics.Count / 2).Average(m => m.ProductivityScore);

        var change = secondHalf - firstHalf;
        
        return change switch
        {
            > 5 => TrendType.Improving,
            < -5 => TrendType.Declining,
            _ => TrendType.Stable
        };
    }

    /// <summary>
    /// Gera insights baseados nas métricas
    /// </summary>
    private List<string> GenerateInsights(AggregatedMetrics metrics, List<ProductivityMetrics> dailyMetrics)
    {
        var insights = new List<string>();

        if (metrics.AverageProductivityScore >= 80)
        {
            insights.Add("Excelente performance no período");
        }
        else if (metrics.AverageProductivityScore >= 60)
        {
            insights.Add("Performance satisfatória com oportunidades de melhoria");
        }
        else
        {
            insights.Add("Performance abaixo do esperado - revisar rotinas de trabalho");
        }

        if (metrics.TotalActiveTimeMinutes > 0)
        {
            var avgHoursPerDay = (decimal)metrics.TotalActiveTimeMinutes / metrics.WorkingDays / 60;
            if (avgHoursPerDay > 8)
            {
                insights.Add($"Trabalhando em média {avgHoursPerDay:F1} horas por dia - considerar melhor work-life balance");
            }
        }

        if (metrics.TopApplications.Any())
        {
            var topApp = metrics.TopApplications.First();
            insights.Add($"Aplicação mais utilizada: {topApp.ApplicationName} ({topApp.TimePercentage:F1}% do tempo)");
        }

        return insights;
    }

    /// <summary>
    /// Gera resumo das principais mudanças
    /// </summary>
    private List<string> GenerateKeyChanges(MetricsComparison comparison)
    {
        var changes = new List<string>();

        if (Math.Abs(comparison.ProductivityChange) > 5)
        {
            var direction = comparison.ProductivityChange > 0 ? "aumentou" : "diminuiu";
            changes.Add($"Produtividade {direction} {Math.Abs(comparison.ProductivityChange):F1}%");
        }

        if (Math.Abs(comparison.ActivityChange) > 5)
        {
            var direction = comparison.ActivityChange > 0 ? "aumentou" : "diminuiu";
            changes.Add($"Atividade {direction} {Math.Abs(comparison.ActivityChange):F1}%");
        }

        if (Math.Abs(comparison.ActiveTimeChange) > 30)
        {
            var direction = comparison.ActiveTimeChange > 0 ? "aumentou" : "diminuiu";
            changes.Add($"Tempo ativo {direction} {Math.Abs(comparison.ActiveTimeChange)} minutos");
        }

        if (!changes.Any())
        {
            changes.Add("Métricas mantiveram-se estáveis");
        }

        return changes;
    }
}