using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;

namespace EAM.Infrastructure.Cache.Services;

/// <summary>
/// Serviço para limpeza automática do cache
/// </summary>
public class CacheCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<CacheCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1); // Executar limpeza a cada hora

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="serviceProvider">Provedor de serviços</param>
    /// <param name="cacheSettings">Configurações de cache</param>
    /// <param name="logger">Logger</param>
    public CacheCleanupService(
        IServiceProvider serviceProvider,
        IOptions<CacheSettings> cacheSettings,
        ILogger<CacheCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executa a limpeza automática do cache
    /// </summary>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguardar um pouco para garantir que todos os serviços estão prontos
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Iniciando limpeza automática do cache...");

                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                // Executar diferentes tipos de limpeza
                await CleanupExpiredEntries(cacheService, stoppingToken);
                await CleanupTemporaryData(cacheService, stoppingToken);
                await CleanupOldMetrics(cacheService, stoppingToken);
                await CleanupOrphanedData(cacheService, stoppingToken);

                // Executar compactação se necessário
                await CompactCacheIfNeeded(cacheService, stoppingToken);

                _logger.LogDebug("Limpeza automática do cache concluída");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante a limpeza automática do cache");
            }

            // Aguardar próximo ciclo de limpeza
            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Limpa entradas expiradas do cache
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task CleanupExpiredEntries(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogDebug("Limpando entradas expiradas do cache...");

            // Padrões de chaves que podem ter expirado
            var expiredPatterns = new[]
            {
                "session:*",
                "temp:*",
                "upload:*",
                "download:*"
            };

            var totalCleaned = 0;

            foreach (var pattern in expiredPatterns)
            {
                var cleaned = await cacheService.RemoveByPatternAsync(pattern, stoppingToken);
                totalCleaned += cleaned;
            }

            if (totalCleaned > 0)
            {
                _logger.LogInformation("Removidas {TotalCleaned} entradas expiradas do cache", totalCleaned);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar entradas expiradas");
        }
    }

    /// <summary>
    /// Limpa dados temporários
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task CleanupTemporaryData(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogDebug("Limpando dados temporários...");

            // Limpar dados temporários antigos (mais de 1 hora)
            var temporaryPatterns = new[]
            {
                "temp:*",
                "cache:temp:*",
                "processing:*",
                "batch:*"
            };

            var totalCleaned = 0;

            foreach (var pattern in temporaryPatterns)
            {
                var cleaned = await cacheService.RemoveByPatternAsync(pattern, stoppingToken);
                totalCleaned += cleaned;
            }

            if (totalCleaned > 0)
            {
                _logger.LogInformation("Removidos {TotalCleaned} dados temporários do cache", totalCleaned);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar dados temporários");
        }
    }

    /// <summary>
    /// Limpa métricas antigas
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task CleanupOldMetrics(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogDebug("Limpando métricas antigas...");

            // Limpar métricas antigas (mais de 24 horas)
            var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            var oldMetricsPatterns = new[]
            {
                $"metrics:daily:{yesterday}:*",
                $"metrics:hourly:{yesterday}:*",
                "metrics:temp:*"
            };

            var totalCleaned = 0;

            foreach (var pattern in oldMetricsPatterns)
            {
                var cleaned = await cacheService.RemoveByPatternAsync(pattern, stoppingToken);
                totalCleaned += cleaned;
            }

            if (totalCleaned > 0)
            {
                _logger.LogInformation("Removidas {TotalCleaned} métricas antigas do cache", totalCleaned);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar métricas antigas");
        }
    }

    /// <summary>
    /// Limpa dados órfãos
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task CleanupOrphanedData(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogDebug("Limpando dados órfãos...");

            // Limpar dados de agentes que não existem mais
            var orphanedPatterns = new[]
            {
                "agent:*:disconnected",
                "agent:*:timeout",
                "session:*:expired"
            };

            var totalCleaned = 0;

            foreach (var pattern in orphanedPatterns)
            {
                var cleaned = await cacheService.RemoveByPatternAsync(pattern, stoppingToken);
                totalCleaned += cleaned;
            }

            if (totalCleaned > 0)
            {
                _logger.LogInformation("Removidos {TotalCleaned} dados órfãos do cache", totalCleaned);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar dados órfãos");
        }
    }

    /// <summary>
    /// Executa compactação do cache se necessário
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task CompactCacheIfNeeded(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogDebug("Verificando necessidade de compactação do cache...");

            // Obter estatísticas do cache
            var statistics = await cacheService.GetStatisticsAsync(stoppingToken);

            // Se o uso de memória estiver alto, considerar limpeza mais agressiva
            if (statistics.MemoryUsagePercentage > 0.8)
            {
                _logger.LogWarning("Uso de memória do cache alto ({MemoryUsage:P2}), executando limpeza adicional...",
                    statistics.MemoryUsagePercentage);

                // Limpar dados menos críticos
                await CleanupLowPriorityData(cacheService, stoppingToken);
            }

            // Se a taxa de hit estiver muito baixa, limpar dados não utilizados
            if (statistics.HitRate < 0.5)
            {
                _logger.LogWarning("Taxa de hit do cache baixa ({HitRate:P2}), limpando dados não utilizados...",
                    statistics.HitRate);

                await CleanupUnusedData(cacheService, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar compactação do cache");
        }
    }

    /// <summary>
    /// Limpa dados de baixa prioridade
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task CleanupLowPriorityData(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            // Dados de baixa prioridade que podem ser removidos em caso de pressão de memória
            var lowPriorityPatterns = new[]
            {
                "cache:optional:*",
                "cache:backup:*",
                "analytics:temp:*",
                "reports:temp:*"
            };

            var totalCleaned = 0;

            foreach (var pattern in lowPriorityPatterns)
            {
                var cleaned = await cacheService.RemoveByPatternAsync(pattern, stoppingToken);
                totalCleaned += cleaned;
            }

            if (totalCleaned > 0)
            {
                _logger.LogInformation("Removidos {TotalCleaned} dados de baixa prioridade do cache", totalCleaned);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar dados de baixa prioridade");
        }
    }

    /// <summary>
    /// Limpa dados não utilizados
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task CleanupUnusedData(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            // Dados que provavelmente não estão sendo utilizados
            var unusedPatterns = new[]
            {
                "cache:old:*",
                "cache:unused:*",
                "lookup:outdated:*"
            };

            var totalCleaned = 0;

            foreach (var pattern in unusedPatterns)
            {
                var cleaned = await cacheService.RemoveByPatternAsync(pattern, stoppingToken);
                totalCleaned += cleaned;
            }

            if (totalCleaned > 0)
            {
                _logger.LogInformation("Removidos {TotalCleaned} dados não utilizados do cache", totalCleaned);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar dados não utilizados");
        }
    }
}