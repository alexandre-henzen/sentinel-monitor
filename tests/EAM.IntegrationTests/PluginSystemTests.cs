using EAM.PluginSDK;
using EAM.Shared.DTOs;
using EAM.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace EAM.IntegrationTests;

/// <summary>
/// Testes do sistema de plugins dinâmicos do EAM
/// Valida carregamento, execução e isolamento de plugins
/// </summary>
[Collection("Integration")]
public class PluginSystemTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<PluginSystemTests> _logger;

    public PluginSystemTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = _fixture.Logger<PluginSystemTests>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.StartInfrastructureAsync();
        await _fixture.StartApiAsync();
        await _fixture.StartAgentAsync();
        await Task.Delay(5000);
    }

    public async Task DisposeAsync()
    {
        await _fixture.StopAgentAsync();
        await _fixture.StopApiAsync();
        await _fixture.StopInfrastructureAsync();
    }

    [Fact]
    public async Task Plugin_ShouldLoadAndExecuteSuccessfully()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var pluginName = "TestCustomTracker";
        var pluginPath = await CreateTestPluginAsync(pluginName);

        _output.WriteLine($"Testando carregamento do plugin {pluginName}");

        // Act 1: Instalar plugin
        var installResponse = await InstallPluginAsync(agentId, pluginPath);
        installResponse.Should().BeTrue();

        // Act 2: Verificar se plugin foi carregado
        var loadedPlugins = await GetLoadedPluginsAsync(agentId);
        loadedPlugins.Should().Contain(p => p.Name == pluginName);

        // Act 3: Executar plugin
        await TriggerPluginExecutionAsync(agentId, pluginName);

        // Act 4: Verificar eventos gerados pelo plugin
        await Task.Delay(5000); // Aguarda execução
        var pluginEvents = await GetPluginEventsAsync(agentId, pluginName);
        
        // Assert: Plugin executou e gerou eventos
        pluginEvents.Should().NotBeEmpty();
        pluginEvents.First().Metadata.Should().ContainKey("pluginName");
        pluginEvents.First().Metadata["pluginName"].ToString().Should().Be(pluginName);

        _output.WriteLine($"Plugin {pluginName} executou com sucesso e gerou {pluginEvents.Count} eventos");
    }

    [Fact]
    public async Task Plugin_ShouldExecuteInIsolatedContext()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var plugin1Name = "IsolatedPlugin1";
        var plugin2Name = "IsolatedPlugin2";
        
        var plugin1Path = await CreateTestPluginAsync(plugin1Name, "IsolatedTracker1");
        var plugin2Path = await CreateTestPluginAsync(plugin2Name, "IsolatedTracker2");

        _output.WriteLine($"Testando isolamento entre plugins {plugin1Name} e {plugin2Name}");

        // Act 1: Instalar ambos os plugins
        var install1 = await InstallPluginAsync(agentId, plugin1Path);
        var install2 = await InstallPluginAsync(agentId, plugin2Path);
        
        install1.Should().BeTrue();
        install2.Should().BeTrue();

        // Act 2: Executar ambos os plugins
        await TriggerPluginExecutionAsync(agentId, plugin1Name);
        await TriggerPluginExecutionAsync(agentId, plugin2Name);

        await Task.Delay(5000);

        // Act 3: Verificar eventos de cada plugin
        var plugin1Events = await GetPluginEventsAsync(agentId, plugin1Name);
        var plugin2Events = await GetPluginEventsAsync(agentId, plugin2Name);

        // Assert: Cada plugin gerou seus próprios eventos
        plugin1Events.Should().NotBeEmpty();
        plugin2Events.Should().NotBeEmpty();
        plugin1Events.Should().OnlyContain(e => e.Metadata["pluginName"].ToString() == plugin1Name);
        plugin2Events.Should().OnlyContain(e => e.Metadata["pluginName"].ToString() == plugin2Name);

        // Act 4: Verificar isolamento de memória
        var plugin1Memory = await GetPluginMemoryUsageAsync(agentId, plugin1Name);
        var plugin2Memory = await GetPluginMemoryUsageAsync(agentId, plugin2Name);

        plugin1Memory.Should().BeGreaterThan(0);
        plugin2Memory.Should().BeGreaterThan(0);

        _output.WriteLine($"Plugins executaram em isolamento: {plugin1Name}={plugin1Memory}KB, {plugin2Name}={plugin2Memory}KB");
    }

    [Fact]
    public async Task Plugin_ShouldHandleRuntimeErrors()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var faultyPluginName = "FaultyPlugin";
        var faultyPluginPath = await CreateFaultyPluginAsync(faultyPluginName);

        _output.WriteLine($"Testando tratamento de erros do plugin {faultyPluginName}");

        // Act 1: Instalar plugin defeituoso
        var installResponse = await InstallPluginAsync(agentId, faultyPluginPath);
        installResponse.Should().BeTrue();

        // Act 2: Tentar executar plugin (deve falhar)
        await TriggerPluginExecutionAsync(agentId, faultyPluginName);

        await Task.Delay(5000);

        // Act 3: Verificar logs de erro
        var errorLogs = await GetPluginErrorLogsAsync(agentId, faultyPluginName);
        errorLogs.Should().NotBeEmpty();
        errorLogs.Should().Contain(log => log.Contains("Plugin execution failed"));

        // Act 4: Verificar que outros plugins não foram afetados
        var systemHealth = await CheckAgentHealthAsync(agentId);
        systemHealth.Should().BeTrue();

        // Act 5: Verificar isolamento de falhas
        var loadedPlugins = await GetLoadedPluginsAsync(agentId);
        var faultyPlugin = loadedPlugins.FirstOrDefault(p => p.Name == faultyPluginName);
        faultyPlugin.Should().NotBeNull();
        faultyPlugin.Status.Should().Be("Error");

        _output.WriteLine($"Plugin defeituoso foi isolado corretamente: {faultyPluginName}");
    }

    [Fact]
    public async Task Plugin_ShouldSupportDynamicReloading()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var pluginName = "ReloadablePlugin";
        var version1Path = await CreateTestPluginAsync(pluginName, "ReloadableTracker", "1.0.0");
        var version2Path = await CreateTestPluginAsync(pluginName, "ReloadableTracker", "2.0.0");

        _output.WriteLine($"Testando recarga dinâmica do plugin {pluginName}");

        // Act 1: Instalar versão 1.0.0
        var install1 = await InstallPluginAsync(agentId, version1Path);
        install1.Should().BeTrue();

        // Act 2: Executar versão 1.0.0
        await TriggerPluginExecutionAsync(agentId, pluginName);
        await Task.Delay(3000);

        var v1Events = await GetPluginEventsAsync(agentId, pluginName);
        v1Events.Should().NotBeEmpty();

        // Act 3: Recarregar com versão 2.0.0
        var reload = await ReloadPluginAsync(agentId, pluginName, version2Path);
        reload.Should().BeTrue();

        // Act 4: Verificar nova versão
        var loadedPlugins = await GetLoadedPluginsAsync(agentId);
        var reloadedPlugin = loadedPlugins.FirstOrDefault(p => p.Name == pluginName);
        reloadedPlugin.Should().NotBeNull();
        reloadedPlugin.Version.Should().Be("2.0.0");

        // Act 5: Executar nova versão
        await TriggerPluginExecutionAsync(agentId, pluginName);
        await Task.Delay(3000);

        var v2Events = await GetPluginEventsAsync(agentId, pluginName);
        v2Events.Should().NotBeEmpty();
        v2Events.Should().OnlyContain(e => e.Metadata.ContainsKey("pluginVersion") && 
                                          e.Metadata["pluginVersion"].ToString() == "2.0.0");

        _output.WriteLine($"Plugin {pluginName} recarregado com sucesso de v1.0.0 para v2.0.0");
    }

    [Fact]
    public async Task Plugin_ShouldRespectPermissions()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var restrictedPluginName = "RestrictedPlugin";
        var restrictedPluginPath = await CreateRestrictedPluginAsync(restrictedPluginName);

        _output.WriteLine($"Testando permissões do plugin {restrictedPluginName}");

        // Act 1: Instalar plugin com permissões restritas
        var installResponse = await InstallPluginAsync(agentId, restrictedPluginPath);
        installResponse.Should().BeTrue();

        // Act 2: Executar plugin (deve ter acesso limitado)
        await TriggerPluginExecutionAsync(agentId, restrictedPluginName);
        await Task.Delay(3000);

        // Act 3: Verificar que plugin respeitou permissões
        var securityLogs = await GetPluginSecurityLogsAsync(agentId, restrictedPluginName);
        securityLogs.Should().Contain(log => log.Contains("Permission denied"));

        // Act 4: Verificar que operações permitidas funcionaram
        var pluginEvents = await GetPluginEventsAsync(agentId, restrictedPluginName);
        pluginEvents.Should().NotBeEmpty();
        pluginEvents.Should().OnlyContain(e => e.ActivityType == ActivityType.CustomActivity);

        _output.WriteLine($"Plugin {restrictedPluginName} respeitou permissões corretamente");
    }

    [Fact]
    public async Task Plugin_ShouldHandleCustomDataTypes()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var customPluginName = "CustomDataPlugin";
        var customPluginPath = await CreateCustomDataPluginAsync(customPluginName);

        _output.WriteLine($"Testando tipos de dados customizados do plugin {customPluginName}");

        // Act 1: Instalar plugin com tipos customizados
        var installResponse = await InstallPluginAsync(agentId, customPluginPath);
        installResponse.Should().BeTrue();

        // Act 2: Executar plugin
        await TriggerPluginExecutionAsync(agentId, customPluginName);
        await Task.Delay(3000);

        // Act 3: Verificar eventos com dados customizados
        var customEvents = await GetPluginEventsAsync(agentId, customPluginName);
        customEvents.Should().NotBeEmpty();

        var customEvent = customEvents.First();
        customEvent.Metadata.Should().ContainKey("customObject");
        customEvent.Metadata.Should().ContainKey("customArray");
        customEvent.Metadata.Should().ContainKey("customNumber");

        // Act 4: Verificar serialização/deserialização
        var customObject = JsonSerializer.Deserialize<Dictionary<string, object>>(
            customEvent.Metadata["customObject"].ToString());
        customObject.Should().NotBeNull();
        customObject.Should().ContainKey("property1");

        _output.WriteLine($"Plugin {customPluginName} processou tipos customizados corretamente");
    }

    [Fact]
    public async Task Plugin_ShouldSupportConfiguration()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var configurablePluginName = "ConfigurablePlugin";
        var pluginPath = await CreateConfigurablePluginAsync(configurablePluginName);
        
        var pluginConfig = new Dictionary<string, object>
        {
            ["interval"] = 10000,
            ["enableLogging"] = true,
            ["customMessage"] = "Integration Test",
            ["maxEvents"] = 100
        };

        _output.WriteLine($"Testando configuração do plugin {configurablePluginName}");

        // Act 1: Instalar plugin com configuração
        var installResponse = await InstallPluginAsync(agentId, pluginPath, pluginConfig);
        installResponse.Should().BeTrue();

        // Act 2: Verificar configuração aplicada
        var pluginInfo = await GetPluginInfoAsync(agentId, configurablePluginName);
        pluginInfo.Should().NotBeNull();
        pluginInfo.Configuration.Should().NotBeNull();
        pluginInfo.Configuration["interval"].Should().Be(10000);
        pluginInfo.Configuration["enableLogging"].Should().Be(true);

        // Act 3: Executar plugin
        await TriggerPluginExecutionAsync(agentId, configurablePluginName);
        await Task.Delay(3000);

        // Act 4: Verificar que configuração foi usada
        var configEvents = await GetPluginEventsAsync(agentId, configurablePluginName);
        configEvents.Should().NotBeEmpty();
        configEvents.Should().OnlyContain(e => e.Metadata.ContainsKey("configMessage") &&
                                             e.Metadata["configMessage"].ToString() == "Integration Test");

        _output.WriteLine($"Plugin {configurablePluginName} usou configuração corretamente");
    }

    [Fact]
    public async Task Plugin_ShouldSupportMultipleInstances()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var multiPluginName = "MultiInstancePlugin";
        var pluginPath = await CreateMultiInstancePluginAsync(multiPluginName);

        _output.WriteLine($"Testando múltiplas instâncias do plugin {multiPluginName}");

        // Act 1: Instalar múltiplas instâncias
        var config1 = new Dictionary<string, object> { ["instanceId"] = "instance1", ["value"] = 100 };
        var config2 = new Dictionary<string, object> { ["instanceId"] = "instance2", ["value"] = 200 };
        var config3 = new Dictionary<string, object> { ["instanceId"] = "instance3", ["value"] = 300 };

        var install1 = await InstallPluginAsync(agentId, pluginPath, config1, "instance1");
        var install2 = await InstallPluginAsync(agentId, pluginPath, config2, "instance2");
        var install3 = await InstallPluginAsync(agentId, pluginPath, config3, "instance3");

        install1.Should().BeTrue();
        install2.Should().BeTrue();
        install3.Should().BeTrue();

        // Act 2: Executar todas as instâncias
        await TriggerPluginExecutionAsync(agentId, multiPluginName, "instance1");
        await TriggerPluginExecutionAsync(agentId, multiPluginName, "instance2");
        await TriggerPluginExecutionAsync(agentId, multiPluginName, "instance3");

        await Task.Delay(5000);

        // Act 3: Verificar eventos de cada instância
        var allEvents = await GetPluginEventsAsync(agentId, multiPluginName);
        allEvents.Should().NotBeEmpty();

        var instance1Events = allEvents.Where(e => e.Metadata["instanceId"].ToString() == "instance1").ToList();
        var instance2Events = allEvents.Where(e => e.Metadata["instanceId"].ToString() == "instance2").ToList();
        var instance3Events = allEvents.Where(e => e.Metadata["instanceId"].ToString() == "instance3").ToList();

        instance1Events.Should().NotBeEmpty();
        instance2Events.Should().NotBeEmpty();
        instance3Events.Should().NotBeEmpty();

        // Assert: Cada instância gerou eventos com sua configuração
        instance1Events.Should().OnlyContain(e => e.Metadata["configValue"].ToString() == "100");
        instance2Events.Should().OnlyContain(e => e.Metadata["configValue"].ToString() == "200");
        instance3Events.Should().OnlyContain(e => e.Metadata["configValue"].ToString() == "300");

        _output.WriteLine($"Plugin {multiPluginName} executou {instance1Events.Count + instance2Events.Count + instance3Events.Count} eventos em 3 instâncias");
    }

    // Helper methods for plugin testing
    private async Task<string> CreateTestPluginAsync(string pluginName, string className = "TestTracker", string version = "1.0.0")
    {
        var pluginCode = $@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace TestPlugin
{{
    public class {className} : ITrackerPlugin
    {{
        public string Name => ""{pluginName}"";
        public string Version => ""{version}"";
        public string Description => ""Test plugin for integration testing"";
        public Dictionary<string, object> Configuration {{ get; set; }} = new();

        public async Task<bool> InitializeAsync()
        {{
            return true;
        }}

        public async Task<List<ActivityEvent>> GetEventsAsync()
        {{
            return new List<ActivityEvent>
            {{
                new ActivityEvent
                {{
                    Id = Guid.NewGuid(),
                    ActivityType = ActivityType.CustomActivity,
                    ProcessName = ""test-process"",
                    WindowTitle = ""Test Window"",
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {{
                        [""pluginName""] = Name,
                        [""pluginVersion""] = Version,
                        [""testData""] = ""integration-test""
                    }}
                }}
            }};
        }}

        public async Task<bool> CleanupAsync()
        {{
            return true;
        }}
    }}
}}";

        var pluginPath = Path.Combine(Path.GetTempPath(), $"{pluginName}.dll");
        await CompilePluginAsync(pluginCode, pluginPath);
        return pluginPath;
    }

    private async Task<string> CreateFaultyPluginAsync(string pluginName)
    {
        var pluginCode = $@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace TestPlugin
{{
    public class FaultyTracker : ITrackerPlugin
    {{
        public string Name => ""{pluginName}"";
        public string Version => ""1.0.0"";
        public string Description => ""Faulty plugin for error testing"";
        public Dictionary<string, object> Configuration {{ get; set; }} = new();

        public async Task<bool> InitializeAsync()
        {{
            return true;
        }}

        public async Task<List<ActivityEvent>> GetEventsAsync()
        {{
            throw new InvalidOperationException(""Simulated plugin error"");
        }}

        public async Task<bool> CleanupAsync()
        {{
            return true;
        }}
    }}
}}";

        var pluginPath = Path.Combine(Path.GetTempPath(), $"{pluginName}.dll");
        await CompilePluginAsync(pluginCode, pluginPath);
        return pluginPath;
    }

    private async Task<string> CreateRestrictedPluginAsync(string pluginName)
    {
        var pluginCode = $@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace TestPlugin
{{
    public class RestrictedTracker : ITrackerPlugin
    {{
        public string Name => ""{pluginName}"";
        public string Version => ""1.0.0"";
        public string Description => ""Restricted plugin for permission testing"";
        public Dictionary<string, object> Configuration {{ get; set; }} = new();

        public async Task<bool> InitializeAsync()
        {{
            return true;
        }}

        public async Task<List<ActivityEvent>> GetEventsAsync()
        {{
            try
            {{
                // Tentar acessar recurso restrito
                System.IO.File.ReadAllText(""C:\\Windows\\System32\\config\\SAM"");
            }}
            catch (UnauthorizedAccessException)
            {{
                // Log de segurança seria registrado aqui
            }}

            return new List<ActivityEvent>
            {{
                new ActivityEvent
                {{
                    Id = Guid.NewGuid(),
                    ActivityType = ActivityType.CustomActivity,
                    ProcessName = ""restricted-process"",
                    WindowTitle = ""Restricted Window"",
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {{
                        [""pluginName""] = Name,
                        [""accessLevel""] = ""restricted""
                    }}
                }}
            }};
        }}

        public async Task<bool> CleanupAsync()
        {{
            return true;
        }}
    }}
}}";

        var pluginPath = Path.Combine(Path.GetTempPath(), $"{pluginName}.dll");
        await CompilePluginAsync(pluginCode, pluginPath);
        return pluginPath;
    }

    private async Task<string> CreateCustomDataPluginAsync(string pluginName)
    {
        var pluginCode = $@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace TestPlugin
{{
    public class CustomDataTracker : ITrackerPlugin
    {{
        public string Name => ""{pluginName}"";
        public string Version => ""1.0.0"";
        public string Description => ""Custom data plugin for data type testing"";
        public Dictionary<string, object> Configuration {{ get; set; }} = new();

        public async Task<bool> InitializeAsync()
        {{
            return true;
        }}

        public async Task<List<ActivityEvent>> GetEventsAsync()
        {{
            return new List<ActivityEvent>
            {{
                new ActivityEvent
                {{
                    Id = Guid.NewGuid(),
                    ActivityType = ActivityType.CustomActivity,
                    ProcessName = ""custom-process"",
                    WindowTitle = ""Custom Window"",
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {{
                        [""pluginName""] = Name,
                        [""customObject""] = new {{ property1 = ""value1"", property2 = 42 }},
                        [""customArray""] = new[] {{ 1, 2, 3, 4, 5 }},
                        [""customNumber""] = 3.14159,
                        [""customDate""] = DateTime.UtcNow
                    }}
                }}
            }};
        }}

        public async Task<bool> CleanupAsync()
        {{
            return true;
        }}
    }}
}}";

        var pluginPath = Path.Combine(Path.GetTempPath(), $"{pluginName}.dll");
        await CompilePluginAsync(pluginCode, pluginPath);
        return pluginPath;
    }

    private async Task<string> CreateConfigurablePluginAsync(string pluginName)
    {
        var pluginCode = $@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace TestPlugin
{{
    public class ConfigurableTracker : ITrackerPlugin
    {{
        public string Name => ""{pluginName}"";
        public string Version => ""1.0.0"";
        public string Description => ""Configurable plugin for configuration testing"";
        public Dictionary<string, object> Configuration {{ get; set; }} = new();

        public async Task<bool> InitializeAsync()
        {{
            return true;
        }}

        public async Task<List<ActivityEvent>> GetEventsAsync()
        {{
            var customMessage = Configuration.TryGetValue(""customMessage"", out var msg) ? msg.ToString() : ""default"";
            var enableLogging = Configuration.TryGetValue(""enableLogging"", out var log) && (bool)log;

            return new List<ActivityEvent>
            {{
                new ActivityEvent
                {{
                    Id = Guid.NewGuid(),
                    ActivityType = ActivityType.CustomActivity,
                    ProcessName = ""configurable-process"",
                    WindowTitle = ""Configurable Window"",
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {{
                        [""pluginName""] = Name,
                        [""configMessage""] = customMessage,
                        [""loggingEnabled""] = enableLogging
                    }}
                }}
            }};
        }}

        public async Task<bool> CleanupAsync()
        {{
            return true;
        }}
    }}
}}";

        var pluginPath = Path.Combine(Path.GetTempPath(), $"{pluginName}.dll");
        await CompilePluginAsync(pluginCode, pluginPath);
        return pluginPath;
    }

    private async Task<string> CreateMultiInstancePluginAsync(string pluginName)
    {
        var pluginCode = $@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EAM.PluginSDK;

namespace TestPlugin
{{
    public class MultiInstanceTracker : ITrackerPlugin
    {{
        public string Name => ""{pluginName}"";
        public string Version => ""1.0.0"";
        public string Description => ""Multi-instance plugin for instance testing"";
        public Dictionary<string, object> Configuration {{ get; set; }} = new();

        public async Task<bool> InitializeAsync()
        {{
            return true;
        }}

        public async Task<List<ActivityEvent>> GetEventsAsync()
        {{
            var instanceId = Configuration.TryGetValue(""instanceId"", out var id) ? id.ToString() : ""default"";
            var value = Configuration.TryGetValue(""value"", out var val) ? val.ToString() : ""0"";

            return new List<ActivityEvent>
            {{
                new ActivityEvent
                {{
                    Id = Guid.NewGuid(),
                    ActivityType = ActivityType.CustomActivity,
                    ProcessName = ""multi-instance-process"",
                    WindowTitle = ""Multi Instance Window"",
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {{
                        [""pluginName""] = Name,
                        [""instanceId""] = instanceId,
                        [""configValue""] = value
                    }}
                }}
            }};
        }}

        public async Task<bool> CleanupAsync()
        {{
            return true;
        }}
    }}
}}";

        var pluginPath = Path.Combine(Path.GetTempPath(), $"{pluginName}.dll");
        await CompilePluginAsync(pluginCode, pluginPath);
        return pluginPath;
    }

    private async Task CompilePluginAsync(string code, string outputPath)
    {
        // Simulação de compilação - em um teste real seria necessário usar Roslyn
        await File.WriteAllTextAsync(outputPath + ".cs", code);
        
        // Criar um assembly fake para os testes
        var fakeAssembly = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // MZ header
        await File.WriteAllBytesAsync(outputPath, fakeAssembly);
    }

    private async Task<bool> InstallPluginAsync(Guid agentId, string pluginPath, Dictionary<string, object>? config = null, string? instanceId = null)
    {
        var installRequest = new
        {
            AgentId = agentId,
            PluginPath = pluginPath,
            Configuration = config ?? new Dictionary<string, object>(),
            InstanceId = instanceId
        };

        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/agents/plugins/install", installRequest);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> ReloadPluginAsync(Guid agentId, string pluginName, string newPluginPath)
    {
        var reloadRequest = new
        {
            AgentId = agentId,
            PluginName = pluginName,
            NewPluginPath = newPluginPath
        };

        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/agents/plugins/reload", reloadRequest);
        return response.IsSuccessStatusCode;
    }

    private async Task<List<PluginInfo>> GetLoadedPluginsAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}/plugins");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<PluginInfo>>() ?? new List<PluginInfo>();
        }
        return new List<PluginInfo>();
    }

    private async Task TriggerPluginExecutionAsync(Guid agentId, string pluginName, string? instanceId = null)
    {
        var triggerRequest = new
        {
            AgentId = agentId,
            PluginName = pluginName,
            InstanceId = instanceId
        };

        await _fixture.HttpClient.PostAsJsonAsync("/api/agents/plugins/trigger", triggerRequest);
    }

    private async Task<List<EventDto>> GetPluginEventsAsync(Guid agentId, string pluginName)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/events?agentId={agentId}&plugin={pluginName}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<EventDto>>() ?? new List<EventDto>();
        }
        return new List<EventDto>();
    }

    private async Task<List<string>> GetPluginErrorLogsAsync(Guid agentId, string pluginName)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}/plugins/{pluginName}/logs?level=error");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
        }
        return new List<string>();
    }

    private async Task<List<string>> GetPluginSecurityLogsAsync(Guid agentId, string pluginName)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}/plugins/{pluginName}/logs?level=security");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
        }
        return new List<string>();
    }

    private async Task<bool> CheckAgentHealthAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}/health");
        return response.IsSuccessStatusCode;
    }

    private async Task<long> GetPluginMemoryUsageAsync(Guid agentId, string pluginName)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}/plugins/{pluginName}/memory");
        if (response.IsSuccessStatusCode)
        {
            var memoryInfo = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            return memoryInfo != null && memoryInfo.TryGetValue("memoryUsageKB", out var memory) ? 
                   Convert.ToInt64(memory) : 0;
        }
        return 0;
    }

    private async Task<PluginInfo> GetPluginInfoAsync(Guid agentId, string pluginName)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}/plugins/{pluginName}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PluginInfo>();
        }
        return null;
    }
}

public class PluginInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LoadedAt { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public long MemoryUsageKB { get; set; }
    public int EventCount { get; set; }
    public DateTime LastExecution { get; set; }
}