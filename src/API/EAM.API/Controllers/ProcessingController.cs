using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Processing.Models;
using EAM.Infrastructure.Processing.Services;

namespace EAM.API.Controllers;

/// <summary>
/// Controller para monitoramento e controle do processamento assíncrono
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProcessingController : ControllerBase
{
    private readonly IEventIngestionService _eventIngestionService;
    private readonly ILogger<ProcessingController> _logger;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="eventIngestionService">Serviço de ingestão</param>
    /// <param name="logger">Logger</param>
    public ProcessingController(
        IEventIngestionService eventIngestionService,
        ILogger<ProcessingController> logger)
    {
        _eventIngestionService = eventIngestionService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém estatísticas de processamento
    /// </summary>
    /// <returns>Estatísticas de processamento</returns>
    [HttpGet("statistics")]
    public async Task<ActionResult<ProcessingStatistics>> GetStatistics()
    {
        try
        {
            var statistics = await _eventIngestionService.GetProcessingStatisticsAsync();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas de processamento");
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém informações sobre o canal de processamento
    /// </summary>
    /// <returns>Informações do canal</returns>
    [HttpGet("channel")]
    public ActionResult<ChannelInfo> GetChannelInfo()
    {
        try
        {
            var channelInfo = _eventIngestionService.GetChannelInfo();
            return Ok(channelInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter informações do canal");
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém status geral do processamento
    /// </summary>
    /// <returns>Status do processamento</returns>
    [HttpGet("status")]
    public async Task<ActionResult<object>> GetStatus()
    {
        try
        {
            var statistics = await _eventIngestionService.GetProcessingStatisticsAsync();
            var channelInfo = _eventIngestionService.GetChannelInfo();

            var status = new
            {
                IsHealthy = statistics.Status == ProcessingStatus.Healthy,
                Status = statistics.Status.ToString(),
                ChannelStatus = channelInfo.Status.ToString(),
                ChannelUtilization = channelInfo.UtilizationPercentage,
                ConcurrencyUtilization = statistics.ConcurrencyUtilization,
                ProcessingRate = statistics.ProcessingRate,
                SuccessRate = statistics.SuccessRate,
                TotalProcessed = statistics.TotalBatchesProcessed,
                TotalFailed = statistics.TotalFailedBatches,
                LastUpdated = statistics.LastUpdated
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter status do processamento");
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém métricas detalhadas de processamento
    /// </summary>
    /// <returns>Métricas detalhadas</returns>
    [HttpGet("metrics")]
    public async Task<ActionResult<object>> GetMetrics()
    {
        try
        {
            var statistics = await _eventIngestionService.GetProcessingStatisticsAsync();
            var channelInfo = _eventIngestionService.GetChannelInfo();

            var metrics = new
            {
                // Métricas do canal
                Channel = new
                {
                    channelInfo.Capacity,
                    channelInfo.Count,
                    channelInfo.AvailableCapacity,
                    channelInfo.UtilizationPercentage,
                    channelInfo.IsFull,
                    channelInfo.IsCompleted,
                    Status = channelInfo.Status.ToString()
                },

                // Métricas de processamento
                Processing = new
                {
                    statistics.TotalBatchesProcessed,
                    statistics.TotalEventsProcessed,
                    statistics.TotalFailedBatches,
                    statistics.TotalFailedEvents,
                    statistics.AverageProcessingTimeMs,
                    statistics.ProcessingRate,
                    statistics.SuccessRate,
                    statistics.MaxConcurrentBatches,
                    statistics.CurrentConcurrentBatches,
                    statistics.ConcurrencyUtilization,
                    Status = statistics.Status.ToString()
                },

                // Métricas de saúde
                Health = new
                {
                    IsHealthy = statistics.Status == ProcessingStatus.Healthy,
                    ChannelHealthy = channelInfo.Status != ChannelStatus.Full,
                    ConcurrencyHealthy = statistics.ConcurrencyUtilization < 0.9,
                    SuccessRateHealthy = statistics.SuccessRate > 0.95,
                    OverallHealth = DetermineOverallHealth(statistics, channelInfo)
                },

                // Timestamps
                LastUpdated = statistics.LastUpdated,
                CheckTime = DateTime.UtcNow
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter métricas de processamento");
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém resumo executivo do processamento
    /// </summary>
    /// <returns>Resumo executivo</returns>
    [HttpGet("summary")]
    public async Task<ActionResult<object>> GetSummary()
    {
        try
        {
            var statistics = await _eventIngestionService.GetProcessingStatisticsAsync();
            var channelInfo = _eventIngestionService.GetChannelInfo();

            var summary = new
            {
                Summary = statistics.Summary,
                Status = statistics.Status.ToString(),
                ChannelStatus = channelInfo.Status.ToString(),
                KeyMetrics = new
                {
                    TotalProcessed = statistics.TotalBatchesProcessed,
                    SuccessRate = $"{statistics.SuccessRate:P2}",
                    ChannelUtilization = $"{channelInfo.UtilizationPercentage:P1}",
                    ConcurrencyUtilization = $"{statistics.ConcurrencyUtilization:P1}",
                    ProcessingRate = $"{statistics.ProcessingRate:F1} lotes/s"
                },
                Recommendations = GetRecommendations(statistics, channelInfo),
                LastUpdated = statistics.LastUpdated
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter resumo do processamento");
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Reinicia as estatísticas de processamento (apenas para desenvolvimento)
    /// </summary>
    /// <returns>Resultado da operação</returns>
    [HttpPost("reset-statistics")]
    public ActionResult ResetStatistics()
    {
        try
        {
            // Esta operação seria implementada no serviço de processamento
            // Por enquanto, retornamos uma mensagem informativa
            
            _logger.LogWarning("Solicitação de reset das estatísticas de processamento");
            
            return Ok(new { 
                message = "Reset de estatísticas não implementado nesta versão", 
                timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao resetar estatísticas");
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Determina a saúde geral do sistema
    /// </summary>
    /// <param name="statistics">Estatísticas de processamento</param>
    /// <param name="channelInfo">Informações do canal</param>
    /// <returns>Status de saúde geral</returns>
    private static string DetermineOverallHealth(ProcessingStatistics statistics, ChannelInfo channelInfo)
    {
        if (statistics.Status == ProcessingStatus.Critical || channelInfo.Status == ChannelStatus.Full)
            return "Critical";
        
        if (statistics.Status == ProcessingStatus.Warning || channelInfo.Status == ChannelStatus.HighUtilization)
            return "Warning";
        
        if (statistics.Status == ProcessingStatus.Degraded || statistics.ConcurrencyUtilization > 0.8)
            return "Degraded";
        
        return "Healthy";
    }

    /// <summary>
    /// Obtém recomendações baseadas no estado atual
    /// </summary>
    /// <param name="statistics">Estatísticas de processamento</param>
    /// <param name="channelInfo">Informações do canal</param>
    /// <returns>Lista de recomendações</returns>
    private static List<string> GetRecommendations(ProcessingStatistics statistics, ChannelInfo channelInfo)
    {
        var recommendations = new List<string>();

        if (channelInfo.UtilizationPercentage > 0.8)
        {
            recommendations.Add("Canal com alta utilização - considere aumentar a capacidade");
        }

        if (statistics.ConcurrencyUtilization > 0.9)
        {
            recommendations.Add("Processamento concorrente saturado - considere aumentar o número de workers");
        }

        if (statistics.SuccessRate < 0.95)
        {
            recommendations.Add("Taxa de sucesso baixa - verifique logs de erro e conectividade");
        }

        if (statistics.ProcessingRate < 1.0)
        {
            recommendations.Add("Taxa de processamento baixa - verifique performance do banco de dados");
        }

        if (statistics.AverageProcessingTimeMs > 1000)
        {
            recommendations.Add("Tempo de processamento alto - considere otimizar operações de banco");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Sistema funcionando dentro dos parâmetros normais");
        }

        return recommendations;
    }
}