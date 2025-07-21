using EAM.API.Core.Models.HealthCheck;

namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para serviço de health check
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Executa todos os health checks
    /// </summary>
    /// <returns>Resumo geral dos health checks</returns>
    Task<HealthCheckSummary> GetHealthAsync();
    
    /// <summary>
    /// Executa health check específico
    /// </summary>
    /// <param name="serviceName">Nome do serviço</param>
    /// <returns>Resultado do health check</returns>
    Task<HealthCheckResult> GetHealthAsync(string serviceName);
    
    /// <summary>
    /// Executa health check do banco de dados
    /// </summary>
    /// <returns>Resultado do health check</returns>
    Task<HealthCheckResult> CheckDatabaseAsync();
    
    /// <summary>
    /// Executa health check do Redis
    /// </summary>
    /// <returns>Resultado do health check</returns>
    Task<HealthCheckResult> CheckRedisAsync();
    
    /// <summary>
    /// Executa health check do MinIO
    /// </summary>
    /// <returns>Resultado do health check</returns>
    Task<HealthCheckResult> CheckStorageAsync();
    
    /// <summary>
    /// Executa health check do processamento de eventos
    /// </summary>
    /// <returns>Resultado do health check</returns>
    Task<HealthCheckResult> CheckEventProcessingAsync();
    
    /// <summary>
    /// Executa health check da aplicação
    /// </summary>
    /// <returns>Resultado do health check</returns>
    Task<HealthCheckResult> CheckApplicationAsync();
}