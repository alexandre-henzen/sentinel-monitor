using EAM.Agent.Data;
using EAM.Agent.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

namespace EAM.Agent.Services
{
    public class PluginLoaderService : BackgroundService
    {
        private readonly ILogger<PluginLoaderService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<ITrackerPlugin> _plugins = new();
        private readonly string _pluginsPath;

        public PluginLoaderService(
            ILogger<PluginLoaderService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _pluginsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EAM", "plugins");
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PluginLoaderService is starting.");
            await LoadPluginsAsync();
            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("PluginLoaderService running at: {time}", DateTimeOffset.Now);

                foreach (var plugin in _plugins)
                {
                    try
                    {
                        var events = await plugin.PollAsync(stoppingToken);
                        if (events != null && events.Any())
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            await dbContext.ActivityEvents.AddRangeAsync(events, stoppingToken);
                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error polling plugin {PluginName}", plugin.Name);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task LoadPluginsAsync()
        {
            try
            {
                if (!Directory.Exists(_pluginsPath))
                {
                    _logger.LogInformation("Plugins directory not found. Creating it at: {path}", _pluginsPath);
                    Directory.CreateDirectory(_pluginsPath);
                }

                var pluginDlls = Directory.GetFiles(_pluginsPath, "*.dll");
                _logger.LogInformation("Found {count} plugin DLLs to load.", pluginDlls.Length);

                foreach (var dllPath in pluginDlls)
                {
                    try
                    {
                        var context = new AssemblyLoadContext(dllPath, isCollectible: true);
                        var assembly = context.LoadFromAssemblyPath(dllPath);

                        var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(ITrackerPlugin).IsAssignableFrom(t) && !t.IsInterface);

                        if (pluginType != null)
                        {
                            var pluginInstance = Activator.CreateInstance(pluginType) as ITrackerPlugin;
                            if (pluginInstance == null)
                            {
                                _logger.LogError("Could not create an instance of plugin from {dllPath}", dllPath);
                                context.Unload();
                                continue;
                            }

                            // TODO: Create a logger factory for the plugin
                            await pluginInstance.InitializeAsync(_configuration, _logger);
                            _plugins.Add(pluginInstance);
                            _logger.LogInformation("Successfully loaded plugin: {PluginName}", pluginInstance.Name);
                        }
                        else
                        {
                            _logger.LogWarning("Could not find an implementation of ITrackerPlugin in {dllPath}", dllPath);
                            // Since the plugin was not loaded, the context can be unloaded
                            context.Unload();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load plugin from {dllPath}", dllPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A critical error occurred while loading plugins.");
            }
        }
    }
}