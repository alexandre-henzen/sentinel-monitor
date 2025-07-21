using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EAM.Agent;

public class AgentBackgroundService : BackgroundService
{
    private readonly ILogger<AgentBackgroundService> _logger;

    public AgentBackgroundService(ILogger<AgentBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EAM Agent Background Service iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("EAM Agent executando em: {time}", DateTimeOffset.Now);

            // TODO: Implementar lógica de coleta de dados
            await Task.Delay(60000, stoppingToken); // 1 minuto
        }

        _logger.LogInformation("EAM Agent Background Service parando");
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EAM Agent Background Service está parando");
        await base.StopAsync(stoppingToken);
    }
}