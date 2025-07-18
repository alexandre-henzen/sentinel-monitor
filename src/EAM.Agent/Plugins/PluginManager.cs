using EAM.PluginSDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Runtime.Loader;

namespace EAM.Agent.Plugins;

public class PluginManager
{
    private readonly ILogger<PluginManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins;
    private readonly string _pluginDirectory;
    private readonly Timer? _scanTimer;

    public PluginManager(ILogger<PluginManager> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _loadedPlugins = new Dictionary<string, LoadedPlugin>();
        
        _pluginDirectory = Environment.ExpandEnvironmentVariables(
            _configuration["Plugins:Directory"] ?? @"%PROGRAMDATA%\EAM\plugins");
        
        // Criar diretório se não existir
        if (!Directory.Exists(_pluginDirectory))
        {
            Directory.CreateDirectory(_pluginDirectory);
        }

        // Configurar timer para scan periódico
        var scanInterval = _configuration.GetValue<int>("Plugins:ScanIntervalSeconds", 300) * 1000;
        if (scanInterval > 0)
        {
            _scanTimer = new Timer(async _ => await ScanForPluginsAsync(), null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(scanInterval));
        }
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Inicializando Plugin Manager. Diretório: {Directory}", _pluginDirectory);
        
        if (_configuration.GetValue<bool>("Plugins:LoadOnStartup", true))
        {
            await ScanForPluginsAsync();
        }
    }

    public async Task<IEnumerable<ActivityEvent>> PollAllPluginsAsync()
    {
        var events = new List<ActivityEvent>();
        
        foreach (var pluginPair in _loadedPlugins)
        {
            var plugin = pluginPair.Value;
            
            try
            {
                if (await plugin.Plugin.IsEnabledAsync())
                {
                    var pluginEvents = await plugin.Plugin.PollAsync(CancellationToken.None);
                    events.AddRange(pluginEvents);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro executando plugin {PluginName}", plugin.Plugin.Name);
            }
        }
        
        return events;
    }

    private async Task ScanForPluginsAsync()
    {
        try
        {
            _logger.LogDebug("Escaneando diretório de plugins: {Directory}", _pluginDirectory);
            
            var pluginDirectories = Directory.GetDirectories(_pluginDirectory);
            
            foreach (var pluginDir in pluginDirectories)
            {
                await LoadPluginFromDirectoryAsync(pluginDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro escaneando plugins");
        }
    }

    private async Task LoadPluginFromDirectoryAsync(string pluginDirectory)
    {
        try
        {
            var pluginName = Path.GetFileName(pluginDirectory);
            
            // Verificar se plugin já está carregado
            if (_loadedPlugins.ContainsKey(pluginName))
            {
                _logger.LogDebug("Plugin {PluginName} já está carregado", pluginName);
                return;
            }

            // Procurar por plugin.json
            var pluginConfigPath = Path.Combine(pluginDirectory, "plugin.json");
            if (!File.Exists(pluginConfigPath))
            {
                _logger.LogDebug("Plugin {PluginName} não possui plugin.json", pluginName);
                return;
            }

            var pluginConfig = await LoadPluginConfigAsync(pluginConfigPath);
            if (pluginConfig == null)
            {
                _logger.LogWarning("Erro carregando configuração do plugin {PluginName}", pluginName);
                return;
            }

            // Procurar por DLL principal
            var pluginDllPath = Path.Combine(pluginDirectory, $"{pluginName}.dll");
            if (!File.Exists(pluginDllPath))
            {
                _logger.LogWarning("Plugin {PluginName} não possui DLL principal", pluginName);
                return;
            }

            await LoadPluginAssemblyAsync(pluginName, pluginDllPath, pluginConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro carregando plugin do diretório {Directory}", pluginDirectory);
        }
    }

    private async Task<PluginConfig?> LoadPluginConfigAsync(string configPath)
    {
        try
        {
            var jsonContent = await File.ReadAllTextAsync(configPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<PluginConfig>(jsonContent);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro deserializando configuração do plugin");
            return null;
        }
    }

    private async Task LoadPluginAssemblyAsync(string pluginName, string dllPath, PluginConfig config)
    {
        try
        {
            // Criar contexto de carregamento isolado
            var loadContext = new PluginAssemblyLoadContext(pluginName, dllPath);
            
            // Carregar assembly
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);
            
            // Procurar por tipos que implementam ITrackerPlugin
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(ITrackerPlugin).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();
            
            if (!pluginTypes.Any())
            {
                _logger.LogWarning("Plugin {PluginName} não contém implementações de ITrackerPlugin", pluginName);
                return;
            }

            // Usar o primeiro tipo encontrado
            var pluginType = pluginTypes.First();
            var pluginInstance = Activator.CreateInstance(pluginType) as ITrackerPlugin;
            
            if (pluginInstance == null)
            {
                _logger.LogError("Erro criando instância do plugin {PluginName}", pluginName);
                return;
            }

            // Inicializar plugin
            var pluginConfiguration = await CreatePluginConfigurationAsync(pluginName);
            await pluginInstance.InitializeAsync(pluginConfiguration, _logger);
            
            var loadedPlugin = new LoadedPlugin
            {
                Plugin = pluginInstance,
                Config = config,
                LoadContext = loadContext,
                LoadedAt = DateTime.UtcNow
            };
            
            _loadedPlugins[pluginName] = loadedPlugin;
            
            _logger.LogInformation("Plugin {PluginName} v{Version} carregado com sucesso", 
                pluginInstance.Name, pluginInstance.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro carregando assembly do plugin {PluginName}", pluginName);
        }
    }

    private async Task<IConfiguration> CreatePluginConfigurationAsync(string pluginName)
    {
        var configPath = Path.Combine(_pluginDirectory, pluginName, "config.json");
        
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true)
            .AddEnvironmentVariables($"EAM_PLUGIN_{pluginName.ToUpper()}_");
        
        return configBuilder.Build();
    }

    public async Task UnloadPluginAsync(string pluginName)
    {
        if (_loadedPlugins.TryGetValue(pluginName, out var loadedPlugin))
        {
            try
            {
                await loadedPlugin.Plugin.StopAsync();
                loadedPlugin.LoadContext.Unload();
                _loadedPlugins.Remove(pluginName);
                
                _logger.LogInformation("Plugin {PluginName} descarregado", pluginName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro descarregando plugin {PluginName}", pluginName);
            }
        }
    }

    public async Task ReloadPluginAsync(string pluginName)
    {
        if (_loadedPlugins.ContainsKey(pluginName))
        {
            await UnloadPluginAsync(pluginName);
        }
        
        var pluginDirectory = Path.Combine(_pluginDirectory, pluginName);
        await LoadPluginFromDirectoryAsync(pluginDirectory);
    }

    public IEnumerable<PluginInfo> GetLoadedPlugins()
    {
        return _loadedPlugins.Values.Select(p => new PluginInfo
        {
            Name = p.Plugin.Name,
            Version = p.Plugin.Version,
            Description = p.Plugin.Description,
            Author = p.Plugin.Author,
            IsEnabled = p.Plugin.IsEnabled,
            LoadedAt = p.LoadedAt
        });
    }

    public async Task StopAllPluginsAsync()
    {
        var stopTasks = _loadedPlugins.Values.Select(p => p.Plugin.StopAsync().AsTask());
        await Task.WhenAll(stopTasks);
        
        foreach (var plugin in _loadedPlugins.Values)
        {
            plugin.LoadContext.Unload();
        }
        
        _loadedPlugins.Clear();
        _scanTimer?.Dispose();
        
        _logger.LogInformation("Todos os plugins foram parados e descarregados");
    }

    private class LoadedPlugin
    {
        public ITrackerPlugin Plugin { get; set; } = null!;
        public PluginConfig Config { get; set; } = null!;
        public PluginAssemblyLoadContext LoadContext { get; set; } = null!;
        public DateTime LoadedAt { get; set; }
    }

    private class PluginConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string MainAssembly { get; set; } = string.Empty;
        public string[] Dependencies { get; set; } = Array.Empty<string>();
        public bool Enabled { get; set; } = true;
    }

    public class PluginInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DateTime LoadedAt { get; set; }
    }
}