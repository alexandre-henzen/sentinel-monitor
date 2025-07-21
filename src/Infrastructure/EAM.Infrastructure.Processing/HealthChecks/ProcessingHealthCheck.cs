using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Processing.Services;

namespace EAM.Infrastructure.Processing.HealthChecks;

/// <summary>
/// Health check para processamento assíncrono
/// </summary>
public class ProcessingHealthCheck : IHealthCheck
{
    private readonly IEventIngestionService _eventIngestionService;
    private readonly ProcessingSettings _processingSettings;
    private readonly ILogger<ProcessingHealthCheck> _logger;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="eventIngestionService">Serviço de ingestão</param>
    /// <param name="processingSettings">Configurações de processamento</param>
    /// <param name="logger">Logger</param>
    public ProcessingHealthCheck(
        IEventIngestionService eventIngestionService,
        IOptions<ProcessingSettings> processingSettings,
        ILogger<ProcessingHealthCheck> logger)
    {
        _eventIngestionService = eventIngestionService;
        _processingSettings = processingSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executa o health check
    /// </summary>
    /// <param name="context">Contexto do health check</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado do health check</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Obter informações do canal
            var channelInfo = _eventIngestionService.GetChannelInfo();
            
            // Obter estatísticas de processamento
            var processingStats = await _eventIngestionService.GetProcessingStatisticsAsync(cancellationToken);
            
            var responseTime = DateTime.UtcNow - startTime;
            
            var data = new Dictionary<string, object>
            {
                ["channel_capacity"] = channelInfo.Capacity,
                ["channel_count"] = channelInfo.Count,
                ["channel_available_capacity"] = channelInfo.AvailableCapacity,
                ["channel_utilization"] = channelInfo.UtilizationPercentage,
                ["channel_status"] = channelInfo.Status.ToString(),
                ["is_channel_full"] = channelInfo.IsFull,
                ["is_channel_completed"] = channelInfo.IsCompleted,
                ["max_concurrent_batches"] = processingStats.MaxConcurrentBatches,
                ["current_concurrent_batches"] = processingStats.CurrentConcurrentBatches,
                ["concurrency_utilization"] = processingStats.ConcurrencyUtilization,
                ["response_time_ms"] = responseTime.TotalMilliseconds,
                ["check_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };

            // Verificar se o canal está funcionando
            if (channelInfo.IsCompleted && channelInfo.Count > 0)
            {
                _logger.LogWarning("Processing health check: canal completado mas ainda tem itens");
                
                data["warning"] = "Canal completado mas ainda tem itens pendentes";
                
                return HealthCheckResult.Degraded(
                    "Canal de processamento está completado mas ainda tem itens pendentes",
                    data: data);
            }

            // Verificar se o canal está muito cheio
            if (channelInfo.UtilizationPercentage > 0.9)
            {
                _logger.LogWarning("Processing health check: canal com alta utilização ({Utilization:P2})", 
                    channelInfo.UtilizationPercentage);
                
                data["warning"] = "Canal com alta utilização";
                
                return HealthCheckResult.Degraded(
                    $"Canal de processamento com alta utilização ({channelInfo.UtilizationPercentage:P2})",
                    data: data);
            }

            // Verificar se o processamento concorrente está saturado
            if (processingStats.ConcurrencyUtilization > 0.95)
            {
                _logger.LogWarning("Processing health check: processamento concorrente saturado ({Utilization:P2})", 
                    processingStats.ConcurrencyUtilization);
                
                data["warning"] = "Processamento concorrente saturado";
                
                return HealthCheckResult.Degraded(
                    $"Processamento concorrente saturado ({processingStats.ConcurrencyUtilization:P2})",
                    data: data);
            }

            // Verificar tempo de resposta
            if (responseTime.TotalMilliseconds > 1000)
            {
                _logger.LogWarning("Processing health check: tempo de resposta alto ({ResponseTime}ms)", 
                    responseTime.TotalMilliseconds);
                
                data["warning"] = "Tempo de resposta alto";
                
                return HealthCheckResult.Degraded(
                    $"Tempo de resposta alto ({responseTime.TotalMilliseconds:F2}ms)",
                    data: data);
            }

            // Tentar fazer um teste básico de funcionamento
            try
            {
                await TestBasicFunctionality(cancellationToken);
                data["functionality_test"] = "success";
            }
            catch (Exception ex)
            {
                data["functionality_test"] = "failed";
                data["functionality_error"] = ex.Message;
                
                _logger.LogWarning(ex, "Processing health check: teste de funcionalidade falhou");
                
                return HealthCheckResult.Degraded(
                    $"Teste de funcionalidade falhou: {ex.Message}",
                    data: data);
            }

            _logger.LogDebug("Processing health check passou em todos os testes");
            
            return HealthCheckResult.Healthy(
                $"Processamento funcionando corretamente (canal: {channelInfo.Count}/{channelInfo.Capacity}, " +
                $"concorrência: {processingStats.CurrentConcurrentBatches}/{processingStats.MaxConcurrentBatches})",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing health check falhou");
            
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["error_type"] = ex.GetType().Name,
                ["check_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };

            return HealthCheckResult.Unhealthy(
                $"Falha no health check de processamento: {ex.Message}",
                exception: ex,
                data: errorData);
        }
    }

    /// <summary>
    /// Testa funcionalidade básica do processamento
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task TestBasicFunctionality(CancellationToken cancellationToken)
    {
        // Teste simples: verificar se conseguimos obter estatísticas
        var statistics = await _eventIngestionService.GetProcessingStatisticsAsync(cancellationToken);
        
        if (statistics == null)
        {
            throw new InvalidOperationException("Não foi possível obter estatísticas de processamento");
        }

        // Verificar se os valores fazem sentido
        if (statistics.ChannelCapacity <= 0)
        {
            throw new InvalidOperationException("Capacidade do canal inválida");
        }

        if (statistics.MaxConcurrentBatches <= 0)
        {
            throw new InvalidOperationException("Número máximo de lotes concorrentes inválido");
        }

        // Verificar se o canal está acessível
        var channelInfo = _eventIngestionService.GetChannelInfo();
        
        if (channelInfo == null)
        {
            throw new InvalidOperationException("Não foi possível obter informações do canal");
        }

        if (channelInfo.Capacity != statistics.ChannelCapacity)
        {
            throw new InvalidOperationException("Inconsistência nas informações do canal");
        }
    }
}