using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Analytics.Models;

namespace EAM.API.Controllers;

/// <summary>
/// Controller para análise de produtividade e métricas
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IProductivityAnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="analyticsService">Serviço de análise de produtividade</param>
    /// <param name="logger">Logger</param>
    public AnalyticsController(
        IProductivityAnalyticsService analyticsService,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém métricas diárias de produtividade para um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="date">Data para análise (formato: yyyy-MM-dd)</param>
    /// <returns>Métricas diárias</returns>
    [HttpGet("daily/{agentId}")]
    public async Task<ActionResult<ProductivityMetrics>> GetDailyMetrics(
        Guid agentId, 
        [FromQuery] string date)
    {
        try
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                return BadRequest(new { message = "Data deve estar no formato yyyy-MM-dd" });
            }

            var metrics = await _analyticsService.CalculateDailyMetricsAsync(agentId, parsedDate);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter métricas diárias para agente {AgentId} na data {Date}", agentId, date);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém métricas agregadas para um período
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="startDate">Data de início (formato: yyyy-MM-dd)</param>
    /// <param name="endDate">Data de fim (formato: yyyy-MM-dd)</param>
    /// <param name="periodType">Tipo de período (Daily, Weekly, Monthly)</param>
    /// <returns>Métricas agregadas</returns>
    [HttpGet("aggregated/{agentId}")]
    public async Task<ActionResult<AggregatedMetrics>> GetAggregatedMetrics(
        Guid agentId,
        [FromQuery] string startDate,
        [FromQuery] string endDate,
        [FromQuery] PeriodType periodType = PeriodType.Daily)
    {
        try
        {
            if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsedStartDate))
            {
                return BadRequest(new { message = "Data de início deve estar no formato yyyy-MM-dd" });
            }

            if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsedEndDate))
            {
                return BadRequest(new { message = "Data de fim deve estar no formato yyyy-MM-dd" });
            }

            if (parsedStartDate > parsedEndDate)
            {
                return BadRequest(new { message = "Data de início deve ser anterior à data de fim" });
            }

            var metrics = await _analyticsService.CalculateAggregatedMetricsAsync(
                agentId, parsedStartDate, parsedEndDate, periodType);
            
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter métricas agregadas para agente {AgentId}", agentId);
            return StatusCode(500, new { message = "Erro interno do servidor" });
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
    /// <returns>Comparação de métricas</returns>
    [HttpGet("comparison/{agentId}")]
    public async Task<ActionResult<MetricsComparison>> CompareMetrics(
        Guid agentId,
        [FromQuery] string currentStart,
        [FromQuery] string currentEnd,
        [FromQuery] string previousStart,
        [FromQuery] string previousEnd,
        [FromQuery] PeriodType periodType = PeriodType.Weekly)
    {
        try
        {
            // Validar e parsear todas as datas
            if (!DateTime.TryParseExact(currentStart, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var currentStartDate))
            {
                return BadRequest(new { message = "Data de início atual deve estar no formato yyyy-MM-dd" });
            }

            if (!DateTime.TryParseExact(currentEnd, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var currentEndDate))
            {
                return BadRequest(new { message = "Data de fim atual deve estar no formato yyyy-MM-dd" });
            }

            if (!DateTime.TryParseExact(previousStart, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var previousStartDate))
            {
                return BadRequest(new { message = "Data de início anterior deve estar no formato yyyy-MM-dd" });
            }

            if (!DateTime.TryParseExact(previousEnd, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var previousEndDate))
            {
                return BadRequest(new { message = "Data de fim anterior deve estar no formato yyyy-MM-dd" });
            }

            var comparison = await _analyticsService.CompareMetricsAsync(
                agentId, 
                currentStartDate, currentEndDate,
                previousStartDate, previousEndDate,
                periodType);

            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao comparar métricas para agente {AgentId}", agentId);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém métricas semanais para um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="year">Ano</param>
    /// <param name="week">Semana do ano (1-52)</param>
    /// <returns>Métricas semanais</returns>
    [HttpGet("weekly/{agentId}")]
    public async Task<ActionResult<AggregatedMetrics>> GetWeeklyMetrics(
        Guid agentId,
        [FromQuery] int year,
        [FromQuery] int week)
    {
        try
        {
            if (year < 2020 || year > DateTime.Now.Year + 1)
            {
                return BadRequest(new { message = "Ano inválido" });
            }

            if (week < 1 || week > 52)
            {
                return BadRequest(new { message = "Semana deve estar entre 1 e 52" });
            }

            // Calcular datas da semana
            var jan1 = new DateTime(year, 1, 1);
            var daysOffset = DayOfWeek.Monday - jan1.DayOfWeek;
            var firstMonday = jan1.AddDays(daysOffset);
            var startDate = firstMonday.AddDays((week - 1) * 7);
            var endDate = startDate.AddDays(6);

            var metrics = await _analyticsService.CalculateAggregatedMetricsAsync(
                agentId, startDate, endDate, PeriodType.Weekly);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter métricas semanais para agente {AgentId}", agentId);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém métricas mensais para um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="year">Ano</param>
    /// <param name="month">Mês (1-12)</param>
    /// <returns>Métricas mensais</returns>
    [HttpGet("monthly/{agentId}")]
    public async Task<ActionResult<AggregatedMetrics>> GetMonthlyMetrics(
        Guid agentId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        try
        {
            if (year < 2020 || year > DateTime.Now.Year + 1)
            {
                return BadRequest(new { message = "Ano inválido" });
            }

            if (month < 1 || month > 12)
            {
                return BadRequest(new { message = "Mês deve estar entre 1 e 12" });
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var metrics = await _analyticsService.CalculateAggregatedMetricsAsync(
                agentId, startDate, endDate, PeriodType.Monthly);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter métricas mensais para agente {AgentId}", agentId);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém dashboard com métricas resumidas para um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <returns>Dashboard de métricas</returns>
    [HttpGet("dashboard/{agentId}")]
    public async Task<ActionResult<object>> GetDashboard(Guid agentId)
    {
        try
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);
            var thisWeekStart = today.AddDays(-(int)today.DayOfWeek + 1);
            var lastWeekStart = thisWeekStart.AddDays(-7);
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);

            // Métricas de hoje
            var todayMetrics = await _analyticsService.CalculateDailyMetricsAsync(agentId, today);
            
            // Métricas de ontem
            var yesterdayMetrics = await _analyticsService.CalculateDailyMetricsAsync(agentId, yesterday);

            // Métricas da semana atual
            var thisWeekMetrics = await _analyticsService.CalculateAggregatedMetricsAsync(
                agentId, thisWeekStart, today, PeriodType.Weekly);

            // Métricas da semana passada
            var lastWeekMetrics = await _analyticsService.CalculateAggregatedMetricsAsync(
                agentId, lastWeekStart, lastWeekStart.AddDays(6), PeriodType.Weekly);

            // Métricas do mês atual
            var thisMonthMetrics = await _analyticsService.CalculateAggregatedMetricsAsync(
                agentId, thisMonthStart, today, PeriodType.Monthly);

            var dashboard = new
            {
                Today = new
                {
                    Date = today.ToString("yyyy-MM-dd"),
                    ProductivityScore = todayMetrics.ProductivityScore,
                    ActiveTimeHours = todayMetrics.TotalActiveTimeMinutes / 60.0,
                    TopApplication = todayMetrics.TopApplication,
                    EventsCount = todayMetrics.TotalKeyboardEvents + todayMetrics.TotalMouseClicks
                },
                Yesterday = new
                {
                    Date = yesterday.ToString("yyyy-MM-dd"),
                    ProductivityScore = yesterdayMetrics.ProductivityScore,
                    ActiveTimeHours = yesterdayMetrics.TotalActiveTimeMinutes / 60.0,
                    Comparison = new
                    {
                        ProductivityChange = todayMetrics.ProductivityScore - yesterdayMetrics.ProductivityScore,
                        ActiveTimeChange = todayMetrics.TotalActiveTimeMinutes - yesterdayMetrics.TotalActiveTimeMinutes
                    }
                },
                ThisWeek = new
                {
                    StartDate = thisWeekStart.ToString("yyyy-MM-dd"),
                    EndDate = today.ToString("yyyy-MM-dd"),
                    AverageProductivityScore = thisWeekMetrics.AverageProductivityScore,
                    TotalActiveTimeHours = thisWeekMetrics.TotalActiveTimeMinutes / 60.0,
                    WorkingDays = thisWeekMetrics.WorkingDays,
                    TopApplications = thisWeekMetrics.TopApplications.Take(3)
                },
                ThisMonth = new
                {
                    StartDate = thisMonthStart.ToString("yyyy-MM-dd"),
                    EndDate = today.ToString("yyyy-MM-dd"),
                    AverageProductivityScore = thisMonthMetrics.AverageProductivityScore,
                    TotalActiveTimeHours = thisMonthMetrics.TotalActiveTimeMinutes / 60.0,
                    WorkingDays = thisMonthMetrics.WorkingDays,
                    Trend = thisMonthMetrics.Trend.ToString(),
                    Insights = thisMonthMetrics.Insights.Take(3)
                },
                LastUpdated = DateTime.UtcNow
            };

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter dashboard para agente {AgentId}", agentId);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém tendências de produtividade para um agente
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="days">Número de dias para análise (padrão: 30)</param>
    /// <returns>Tendências de produtividade</returns>
    [HttpGet("trends/{agentId}")]
    public async Task<ActionResult<object>> GetProductivityTrends(
        Guid agentId,
        [FromQuery] int days = 30)
    {
        try
        {
            if (days < 7 || days > 365)
            {
                return BadRequest(new { message = "Número de dias deve estar entre 7 e 365" });
            }

            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-days);

            var metrics = await _analyticsService.CalculateAggregatedMetricsAsync(
                agentId, startDate, endDate, PeriodType.Daily);

            // Calcular tendências por semana
            var weeklyTrends = new List<object>();
            var currentWeekStart = startDate;

            while (currentWeekStart <= endDate)
            {
                var weekEnd = currentWeekStart.AddDays(6);
                if (weekEnd > endDate) weekEnd = endDate;

                var weekMetrics = await _analyticsService.CalculateAggregatedMetricsAsync(
                    agentId, currentWeekStart, weekEnd, PeriodType.Weekly);

                weeklyTrends.Add(new
                {
                    WeekStart = currentWeekStart.ToString("yyyy-MM-dd"),
                    WeekEnd = weekEnd.ToString("yyyy-MM-dd"),
                    ProductivityScore = weekMetrics.AverageProductivityScore,
                    ActiveTimeHours = weekMetrics.TotalActiveTimeMinutes / 60.0,
                    WorkingDays = weekMetrics.WorkingDays
                });

                currentWeekStart = currentWeekStart.AddDays(7);
            }

            var trends = new
            {
                Period = new
                {
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd"),
                    Days = days
                },
                Overall = new
                {
                    AverageProductivityScore = metrics.AverageProductivityScore,
                    TotalActiveTimeHours = metrics.TotalActiveTimeMinutes / 60.0,
                    WorkingDays = metrics.WorkingDays,
                    Trend = metrics.Trend.ToString()
                },
                WeeklyTrends = weeklyTrends,
                TopApplications = metrics.TopApplications.Take(5),
                Insights = metrics.Insights
            };

            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter tendências para agente {AgentId}", agentId);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }
}