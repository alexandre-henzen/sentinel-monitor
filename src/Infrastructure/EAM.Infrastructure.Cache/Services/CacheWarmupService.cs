using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;

namespace EAM.Infrastructure.Cache.Services;

/// <summary>
/// Serviço para aquecimento do cache na inicialização
/// </summary>
public class CacheWarmupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<CacheWarmupService> _logger;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="serviceProvider">Provedor de serviços</param>
    /// <param name="cacheSettings">Configurações de cache</param>
    /// <param name="logger">Logger</param>
    public CacheWarmupService(
        IServiceProvider serviceProvider,
        IOptions<CacheSettings> cacheSettings,
        ILogger<CacheWarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executa o aquecimento do cache
    /// </summary>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguardar um pouco para garantir que todos os serviços estão prontos
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        if (stoppingToken.IsCancellationRequested)
            return;

        _logger.LogInformation("Iniciando aquecimento do cache...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // Aquecimento básico - verificar se o cache está responsivo
            await WarmupBasicOperations(cacheService, stoppingToken);

            // Aquecimento de dados frequentemente acessados
            await WarmupFrequentlyAccessedData(cacheService, stoppingToken);

            _logger.LogInformation("Aquecimento do cache concluído com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante o aquecimento do cache");
        }
    }

    /// <summary>
    /// Aquece operações básicas do cache
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task WarmupBasicOperations(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogDebug("Aquecendo operações básicas do cache...");

            // Teste de escrita e leitura
            var testKey = "warmup:test";
            var testValue = "warmup-value";
            
            await cacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1), stoppingToken);
            var retrievedValue = await cacheService.GetAsync<string>(testKey, stoppingToken);
            
            if (retrievedValue == testValue)
            {
                _logger.LogDebug("Operações básicas do cache estão funcionando");
                await cacheService.RemoveAsync(testKey, stoppingToken);
            }
            else
            {
                _logger.LogWarning("Teste de operações básicas do cache falhou");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar operações básicas do cache");
        }
    }

    /// <summary>
    /// Aquece dados frequentemente acessados
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task WarmupFrequentlyAccessedData(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogDebug("Aquecendo dados frequentemente acessados...");

            // Dados de configuração do sistema
            await WarmupSystemConfiguration(cacheService, stoppingToken);

            // Dados de lookup comuns
            await WarmupCommonLookupData(cacheService, stoppingToken);

            // Métricas básicas
            await WarmupBasicMetrics(cacheService, stoppingToken);

            _logger.LogDebug("Aquecimento de dados frequentes concluído");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aquecer dados frequentemente acessados");
        }
    }

    /// <summary>
    /// Aquece configurações do sistema
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task WarmupSystemConfiguration(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            // Configurações básicas do sistema
            var systemConfig = new
            {
                Version = "5.0.0",
                Environment = "Production",
                Features = new[] { "Screenshots", "Analytics", "Reporting" }
            };

            await cacheService.SetAsync("system:config", systemConfig, TimeSpan.FromHours(1), stoppingToken);
            
            _logger.LogDebug("Configurações do sistema carregadas no cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aquecer configurações do sistema");
        }
    }

    /// <summary>
    /// Aquece dados de lookup comuns
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task WarmupCommonLookupData(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            // Tipos de eventos comuns
            var eventTypes = new[]
            {
                "KeyboardInput",
                "MouseInput",
                "ApplicationSwitch",
                "WindowFocus",
                "IdleStart",
                "IdleEnd",
                "ScreenshotCaptured"
            };

            await cacheService.SetAsync("lookup:event-types", eventTypes, TimeSpan.FromHours(6), stoppingToken);

            // Status de agentes
            var agentStatuses = new[]
            {
                "Active",
                "Inactive",
                "Offline",
                "Suspended"
            };

            await cacheService.SetAsync("lookup:agent-statuses", agentStatuses, TimeSpan.FromHours(6), stoppingToken);

            _logger.LogDebug("Dados de lookup comuns carregados no cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aquecer dados de lookup");
        }
    }

    /// <summary>
    /// Aquece métricas básicas
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="stoppingToken">Token de cancelamento</param>
    /// <returns>Task</returns>
    private async Task WarmupBasicMetrics(ICacheService cacheService, CancellationToken stoppingToken)
    {
        try
        {
            // Contadores iniciais
            var counters = new Dictionary<string, long>
            {
                ["events:total"] = 0,
                ["agents:active"] = 0,
                ["screenshots:total"] = 0,
                ["errors:total"] = 0
            };

            foreach (var counter in counters)
            {
                await cacheService.SetAsync($"metrics:{counter.Key}", counter.Value, TimeSpan.FromMinutes(30), stoppingToken);
            }

            _logger.LogDebug("Métricas básicas inicializadas no cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aquecer métricas básicas");
        }
    }
}