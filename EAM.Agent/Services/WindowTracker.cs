using EAM.Agent.Data;
using EAM.Agent.Helpers;
using EAM.Agent.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text;

namespace EAM.Agent.Services
{
    public class WindowTracker : BackgroundService, IActivityTracker
    {
        private readonly ILogger<WindowTracker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public WindowTracker(ILogger<WindowTracker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WindowTracker running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var windowHandle = NativeMethods.GetForegroundWindow();
                    const int nMaxCount = 256;
                    var stringBuilder = new StringBuilder(nMaxCount);

                    if (NativeMethods.GetWindowText(windowHandle, stringBuilder, nMaxCount) > 0)
                    {
                        var windowTitle = stringBuilder.ToString();

                        NativeMethods.GetWindowThreadProcessId(windowHandle, out uint processId);
                        var process = Process.GetProcessById((int)processId);
                        var processName = process.ProcessName;

                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        var activityEvent = new Models.ActivityEvent
                        {
                            EventType = "WindowFocus",
                            Timestamp = DateTime.UtcNow,
                            WindowTitle = windowTitle,
                            ProcessName = processName
                        };

                        dbContext.ActivityEvents.Add(activityEvent);
                        await dbContext.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation("Logged focus on: {WindowTitle}", windowTitle);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting active window title.");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}