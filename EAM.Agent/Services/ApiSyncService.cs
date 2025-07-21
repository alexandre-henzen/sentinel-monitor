using EAM.Agent.Data;
using EAM.Agent.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EAM.Agent.Services
{
    public class ApiSyncService : BackgroundService
    {
        private readonly ILogger<ApiSyncService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(60));

        public ApiSyncService(
            ILogger<ApiSyncService> logger,
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ApiSyncService is starting.");

            while (await _timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                await SynchronizeEventsAsync(stoppingToken);
            }

            _logger.LogInformation("ApiSyncService is stopping.");
        }

        private async Task SynchronizeEventsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting event synchronization cycle.");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var eventsToSync = await dbContext.ActivityEvents
                .OrderBy(e => e.Timestamp.UtcTicks)
                .Take(100)
                .ToListAsync(cancellationToken);

            if (eventsToSync.Count == 0)
            {
                _logger.LogInformation("No events to synchronize.");
                return;
            }

            _logger.LogInformation("Found {EventCount} events to synchronize.", eventsToSync.Count);

            var ndjson = new StringBuilder();
            foreach (var ev in eventsToSync)
            {
                ndjson.AppendLine(JsonSerializer.Serialize(ev));
            }

            var httpClient = _httpClientFactory.CreateClient("ApiClient");
            var content = new StringContent(ndjson.ToString(), Encoding.UTF8, "application/x-ndjson");

            try
            {
                var response = await httpClient.PostAsync("/events", content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent {EventCount} events to the API.", eventsToSync.Count);
                    dbContext.ActivityEvents.RemoveRange(eventsToSync);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Successfully removed synchronized events from the local database.");
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to send events. Status: {StatusCode}. Response: {ResponseBody}", response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while sending events to the API.");
            }
        }
    }
}