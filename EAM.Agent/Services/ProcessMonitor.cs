namespace EAM.Agent.Services
{
    public class ProcessMonitor : BackgroundService, IActivityTracker
    {
        private readonly ILogger<ProcessMonitor> _logger;

        public ProcessMonitor(ILogger<ProcessMonitor> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ProcessMonitor running.");

            stoppingToken.Register(() => _logger.LogInformation("ProcessMonitor is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                // A lógica de monitoramento de processos será adicionada aqui no futuro.
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogInformation("ProcessMonitor stopped.");
        }
    }
}