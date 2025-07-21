using EAM.Infrastructure.Analytics.Models;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para serviço de análise de produtividade
/// </summary>
public interface IProductivityAnalyticsService
{
    /// <summary>
    /// Calcula métricas de produtividade para um agente em uma data específica
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="date">Data para cálculo</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Métricas de produtividade</returns>
    Task<ProductivityMetrics> CalculateDailyMetricsAsync(
        Guid agentId, 
        DateTime date, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula métricas agregadas para um período
    /// </summary>
    /// <param name="agentId">ID do agente</param>
    /// <param name="startDate">Data de início</param>
    /// <param name="endDate">Data de fim</param>
    /// <param name="periodType">Tipo de período</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Métricas agregadas</returns>
    Task<AggregatedMetrics> CalculateAggregatedMetricsAsync(
        Guid agentId, 
        DateTime startDate, 
        DateTime endDate, 
        PeriodType periodType,
        CancellationToken cancellationToken = default);

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
    Task<MetricsComparison> CompareMetricsAsync(
        Guid agentId, 
        DateTime currentStart, 
        DateTime currentEnd,
        DateTime previousStart, 
        DateTime previousEnd,
        PeriodType periodType,
        CancellationToken cancellationToken = default);
}