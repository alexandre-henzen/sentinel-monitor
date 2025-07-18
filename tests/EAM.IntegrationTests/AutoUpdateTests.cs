using EAM.Shared.DTOs;
using EAM.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO.Compression;
using Xunit;
using Xunit.Abstractions;

namespace EAM.IntegrationTests;

/// <summary>
/// Testes do sistema de auto-update do EAM
/// Valida verificação, download, instalação e rollback
/// </summary>
[Collection("Integration")]
public class AutoUpdateTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AutoUpdateTests> _logger;

    public AutoUpdateTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = _fixture.Logger<AutoUpdateTests>();
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
    public async Task AutoUpdate_ShouldCheckForUpdatesRegularly()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var currentVersion = "5.0.0";
        var newVersion = "5.1.0";

        _output.WriteLine($"Testando verificação automática de updates do agente {agentId}");

        // Act 1: Registrar agente com versão atual
        await RegisterAgentAsync(agentId, currentVersion);

        // Act 2: Publicar nova versão
        await PublishNewVersionAsync(newVersion);

        // Act 3: Aguardar verificação automática
        await Task.Delay(30000); // Aguarda intervalo de verificação

        // Act 4: Verificar se agente detectou update
        var updateStatus = await GetAgentUpdateStatusAsync(agentId);
        
        // Assert: Update detectado
        updateStatus.Should().NotBeNull();
        updateStatus.UpdateAvailable.Should().BeTrue();
        updateStatus.LatestVersion.Should().Be(newVersion);
        updateStatus.CurrentVersion.Should().Be(currentVersion);

        _output.WriteLine($"Update detectado: {currentVersion} → {newVersion}");
    }

    [Fact]
    public async Task AutoUpdate_ShouldDownloadUpdatePackage()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var currentVersion = "5.0.0";
        var newVersion = "5.1.0";
        var updatePackage = await CreateUpdatePackageAsync(newVersion);

        _output.WriteLine($"Testando download de pacote de update para agente {agentId}");

        // Act 1: Registrar agente
        await RegisterAgentAsync(agentId, currentVersion);

        // Act 2: Publicar update com pacote
        await PublishUpdateWithPackageAsync(newVersion, updatePackage);

        // Act 3: Iniciar download
        var downloadResult = await StartUpdateDownloadAsync(agentId, newVersion);
        downloadResult.Should().BeTrue();

        // Act 4: Aguardar download completo
        await WaitForDownloadCompletionAsync(agentId, newVersion);

        // Act 5: Verificar download
        var downloadStatus = await GetDownloadStatusAsync(agentId, newVersion);
        
        // Assert: Download bem-sucedido
        downloadStatus.Should().NotBeNull();
        downloadStatus.IsComplete.Should().BeTrue();
        downloadStatus.IsValid.Should().BeTrue();
        downloadStatus.FileSize.Should().BeGreaterThan(0);
        downloadStatus.ChecksumValid.Should().BeTrue();

        _output.WriteLine($"Download concluído: {downloadStatus.FileSize} bytes");
    }

    [Fact]
    public async Task AutoUpdate_ShouldInstallUpdateSilently()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var currentVersion = "5.0.0";
        var newVersion = "5.1.0";
        var updatePackage = await CreateUpdatePackageAsync(newVersion);

        _output.WriteLine($"Testando instalação silenciosa de update para agente {agentId}");

        // Act 1: Preparar update
        await RegisterAgentAsync(agentId, currentVersion);
        await PublishUpdateWithPackageAsync(newVersion, updatePackage);
        await StartUpdateDownloadAsync(agentId, newVersion);
        await WaitForDownloadCompletionAsync(agentId, newVersion);

        // Act 2: Iniciar instalação
        var installResult = await StartUpdateInstallationAsync(agentId, newVersion);
        installResult.Should().BeTrue();

        // Act 3: Aguardar instalação
        await WaitForInstallationCompletionAsync(agentId, newVersion);

        // Act 4: Verificar instalação
        var installStatus = await GetInstallationStatusAsync(agentId, newVersion);
        
        // Assert: Instalação bem-sucedida
        installStatus.Should().NotBeNull();
        installStatus.IsComplete.Should().BeTrue();
        installStatus.IsSuccessful.Should().BeTrue();
        installStatus.NewVersion.Should().Be(newVersion);

        // Act 5: Verificar se agente reiniciou com nova versão
        await Task.Delay(10000); // Aguarda reinicialização
        var agentInfo = await GetAgentInfoAsync(agentId);
        agentInfo.Should().NotBeNull();
        agentInfo.Version.Should().Be(newVersion);

        _output.WriteLine($"Instalação concluída: agente atualizado para v{newVersion}");
    }

    [Fact]
    public async Task AutoUpdate_ShouldRollbackOnFailure()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var currentVersion = "5.0.0";
        var faultyVersion = "5.1.0";
        var faultyPackage = await CreateFaultyUpdatePackageAsync(faultyVersion);

        _output.WriteLine($"Testando rollback automático para agente {agentId}");

        // Act 1: Preparar update defeituoso
        await RegisterAgentAsync(agentId, currentVersion);
        await PublishUpdateWithPackageAsync(faultyVersion, faultyPackage);
        await StartUpdateDownloadAsync(agentId, faultyVersion);
        await WaitForDownloadCompletionAsync(agentId, faultyVersion);

        // Act 2: Tentar instalar update defeituoso
        var installResult = await StartUpdateInstallationAsync(agentId, faultyVersion);
        installResult.Should().BeTrue();

        // Act 3: Aguardar falha e rollback
        await WaitForRollbackCompletionAsync(agentId, faultyVersion);

        // Act 4: Verificar status do rollback
        var rollbackStatus = await GetRollbackStatusAsync(agentId);
        
        // Assert: Rollback executado
        rollbackStatus.Should().NotBeNull();
        rollbackStatus.RollbackExecuted.Should().BeTrue();
        rollbackStatus.RollbackSuccessful.Should().BeTrue();
        rollbackStatus.RestoredVersion.Should().Be(currentVersion);

        // Act 5: Verificar se agente voltou à versão anterior
        await Task.Delay(10000); // Aguarda estabilização
        var agentInfo = await GetAgentInfoAsync(agentId);
        agentInfo.Should().NotBeNull();
        agentInfo.Version.Should().Be(currentVersion);

        _output.WriteLine($"Rollback concluído: agente restaurado para v{currentVersion}");
    }

    [Fact]
    public async Task AutoUpdate_ShouldValidateUpdateIntegrity()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var currentVersion = "5.0.0";
        var newVersion = "5.1.0";
        var corruptedPackage = await CreateCorruptedUpdatePackageAsync(newVersion);

        _output.WriteLine($"Testando validação de integridade para agente {agentId}");

        // Act 1: Preparar update corrompido
        await RegisterAgentAsync(agentId, currentVersion);
        await PublishUpdateWithPackageAsync(newVersion, corruptedPackage);

        // Act 2: Tentar download
        var downloadResult = await StartUpdateDownloadAsync(agentId, newVersion);
        downloadResult.Should().BeTrue();

        // Act 3: Aguardar validação
        await WaitForDownloadCompletionAsync(agentId, newVersion);

        // Act 4: Verificar detecção de corrupção
        var downloadStatus = await GetDownloadStatusAsync(agentId, newVersion);
        
        // Assert: Corrupção detectada
        downloadStatus.Should().NotBeNull();
        downloadStatus.IsComplete.Should().BeTrue();
        downloadStatus.IsValid.Should().BeFalse();
        downloadStatus.ChecksumValid.Should().BeFalse();

        // Act 5: Verificar que instalação não foi iniciada
        var installStatus = await GetInstallationStatusAsync(agentId, newVersion);
        installStatus.Should().BeNull();

        _output.WriteLine($"Integridade validada: pacote corrompido rejeitado");
    }

    [Fact]
    public async Task AutoUpdate_ShouldHandleNetworkFailures()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var currentVersion = "5.0.0";
        var newVersion = "5.1.0";
        var updatePackage = await CreateUpdatePackageAsync(newVersion);

        _output.WriteLine($"Testando resiliência a falhas de rede para agente {agentId}");

        // Act 1: Preparar update
        await RegisterAgentAsync(agentId, currentVersion);
        await PublishUpdateWithPackageAsync(newVersion, updatePackage);

        // Act 2: Iniciar download
        var downloadResult = await StartUpdateDownloadAsync(agentId, newVersion);
        downloadResult.Should().BeTrue();

        // Act 3: Simular falha de rede durante download
        await SimulateNetworkFailureAsync();
        await Task.Delay(10000);

        // Act 4: Restaurar rede
        await RestoreNetworkAsync();

        // Act 5: Aguardar retry automático
        await WaitForDownloadCompletionAsync(agentId, newVersion);

        // Act 6: Verificar recuperação
        var downloadStatus = await GetDownloadStatusAsync(agentId, newVersion);
        
        // Assert: Download recuperado
        downloadStatus.Should().NotBeNull();
        downloadStatus.IsComplete.Should().BeTrue();
        downloadStatus.IsValid.Should().BeTrue();
        downloadStatus.RetryCount.Should().BeGreaterThan(0);

        _output.WriteLine($"Recuperação de rede bem-sucedida: {downloadStatus.RetryCount} tentativas");
    }

    [Fact]
    public async Task AutoUpdate_ShouldScheduleMaintenanceWindow()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var currentVersion = "5.0.0";
        var newVersion = "5.1.0";
        var updatePackage = await CreateUpdatePackageAsync(newVersion);
        var maintenanceWindow = DateTime.UtcNow.AddHours(2);

        _output.WriteLine($"Testando janela de manutenção para agente {agentId}");

        // Act 1: Preparar update com janela de manutenção
        await RegisterAgentAsync(agentId, currentVersion);
        await PublishUpdateWithMaintenanceWindowAsync(newVersion, updatePackage, maintenanceWindow);

        // Act 2: Verificar que download iniciou mas instalação foi agendada
        var downloadResult = await StartUpdateDownloadAsync(agentId, newVersion);
        downloadResult.Should().BeTrue();

        await WaitForDownloadCompletionAsync(agentId, newVersion);

        // Act 3: Verificar agendamento
        var scheduleStatus = await GetUpdateScheduleStatusAsync(agentId, newVersion);
        
        // Assert: Instalação agendada
        scheduleStatus.Should().NotBeNull();
        scheduleStatus.IsScheduled.Should().BeTrue();
        scheduleStatus.ScheduledTime.Should().BeCloseTo(maintenanceWindow, TimeSpan.FromMinutes(1));

        // Act 4: Simular chegada da janela de manutenção
        await SimulateMaintenanceWindowAsync(agentId, newVersion);

        // Act 5: Verificar que instalação foi executada
        await WaitForInstallationCompletionAsync(agentId, newVersion);
        var installStatus = await GetInstallationStatusAsync(agentId, newVersion);
        
        // Assert: Instalação executada na janela correta
        installStatus.Should().NotBeNull();
        installStatus.IsComplete.Should().BeTrue();
        installStatus.IsSuccessful.Should().BeTrue();

        _output.WriteLine($"Janela de manutenção respeitada: instalação executada no horário agendado");
    }

    [Fact]
    public async Task AutoUpdate_ShouldPreserveUserData()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var currentVersion = "5.0.0";
        var newVersion = "5.1.0";
        var updatePackage = await CreateUpdatePackageAsync(newVersion);
        var userData = await CreateUserDataAsync(agentId);

        _output.WriteLine($"Testando preservação de dados do usuário para agente {agentId}");

        // Act 1: Preparar dados do usuário
        await RegisterAgentAsync(agentId, currentVersion);
        await SaveUserDataAsync(agentId, userData);

        // Act 2: Executar update completo
        await PublishUpdateWithPackageAsync(newVersion, updatePackage);
        await StartUpdateDownloadAsync(agentId, newVersion);
        await WaitForDownloadCompletionAsync(agentId, newVersion);
        await StartUpdateInstallationAsync(agentId, newVersion);
        await WaitForInstallationCompletionAsync(agentId, newVersion);

        // Act 3: Verificar preservação dos dados
        await Task.Delay(10000); // Aguarda reinicialização
        var preservedData = await GetUserDataAsync(agentId);
        
        // Assert: Dados preservados
        preservedData.Should().NotBeNull();
        preservedData.Should().BeEquivalentTo(userData);

        _output.WriteLine($"Dados do usuário preservados: {preservedData.Count} itens");
    }

    // Helper methods
    private async Task RegisterAgentAsync(Guid agentId, string version)
    {
        var agent = new AgentDto
        {
            Id = agentId,
            MachineName = "UPDATE-TEST",
            Username = "update-user",
            Version = version,
            Status = AgentStatus.Online,
            LastHeartbeat = DateTime.UtcNow
        };

        await _fixture.HttpClient.PostAsJsonAsync("/api/agents/register", agent);
    }

    private async Task PublishNewVersionAsync(string version)
    {
        var versionInfo = new
        {
            Version = version,
            ReleaseDate = DateTime.UtcNow,
            IsRequired = false,
            MinimumVersion = "5.0.0"
        };

        await _fixture.HttpClient.PostAsJsonAsync("/api/updates/publish", versionInfo);
    }

    private async Task PublishUpdateWithPackageAsync(string version, byte[] package)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(version), "version");
        formData.Add(new StringContent(DateTime.UtcNow.ToString()), "releaseDate");
        formData.Add(new ByteArrayContent(package), "package", $"EAM.Agent.{version}.msi");

        await _fixture.HttpClient.PostAsync("/api/updates/publish-with-package", formData);
    }

    private async Task PublishUpdateWithMaintenanceWindowAsync(string version, byte[] package, DateTime maintenanceWindow)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(version), "version");
        formData.Add(new StringContent(DateTime.UtcNow.ToString()), "releaseDate");
        formData.Add(new StringContent(maintenanceWindow.ToString()), "maintenanceWindow");
        formData.Add(new ByteArrayContent(package), "package", $"EAM.Agent.{version}.msi");

        await _fixture.HttpClient.PostAsync("/api/updates/publish-with-maintenance", formData);
    }

    private async Task<UpdateStatusDto> GetAgentUpdateStatusAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}/update-status");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UpdateStatusDto>();
        }
        return null;
    }

    private async Task<bool> StartUpdateDownloadAsync(Guid agentId, string version)
    {
        var request = new { AgentId = agentId, Version = version };
        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/updates/start-download", request);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> StartUpdateInstallationAsync(Guid agentId, string version)
    {
        var request = new { AgentId = agentId, Version = version };
        var response = await _fixture.HttpClient.PostAsJsonAsync("/api/updates/start-installation", request);
        return response.IsSuccessStatusCode;
    }

    private async Task WaitForDownloadCompletionAsync(Guid agentId, string version)
    {
        var maxAttempts = 30;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            var status = await GetDownloadStatusAsync(agentId, version);
            if (status?.IsComplete == true)
            {
                return;
            }

            await Task.Delay(2000);
            attempt++;
        }

        throw new TimeoutException("Download não completou no tempo esperado");
    }

    private async Task WaitForInstallationCompletionAsync(Guid agentId, string version)
    {
        var maxAttempts = 60;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            var status = await GetInstallationStatusAsync(agentId, version);
            if (status?.IsComplete == true)
            {
                return;
            }

            await Task.Delay(2000);
            attempt++;
        }

        throw new TimeoutException("Instalação não completou no tempo esperado");
    }

    private async Task WaitForRollbackCompletionAsync(Guid agentId, string version)
    {
        var maxAttempts = 30;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            var status = await GetRollbackStatusAsync(agentId);
            if (status?.RollbackExecuted == true)
            {
                return;
            }

            await Task.Delay(2000);
            attempt++;
        }

        throw new TimeoutException("Rollback não completou no tempo esperado");
    }

    private async Task<DownloadStatusDto> GetDownloadStatusAsync(Guid agentId, string version)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/updates/{agentId}/{version}/download-status");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<DownloadStatusDto>();
        }
        return null;
    }

    private async Task<InstallationStatusDto> GetInstallationStatusAsync(Guid agentId, string version)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/updates/{agentId}/{version}/installation-status");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<InstallationStatusDto>();
        }
        return null;
    }

    private async Task<RollbackStatusDto> GetRollbackStatusAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/updates/{agentId}/rollback-status");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<RollbackStatusDto>();
        }
        return null;
    }

    private async Task<UpdateScheduleStatusDto> GetUpdateScheduleStatusAsync(Guid agentId, string version)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/updates/{agentId}/{version}/schedule-status");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UpdateScheduleStatusDto>();
        }
        return null;
    }

    private async Task<AgentDto> GetAgentInfoAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AgentDto>();
        }
        return null;
    }

    private async Task<byte[]> CreateUpdatePackageAsync(string version)
    {
        // Criar um pacote MSI simulado
        var packageData = new byte[1024 * 1024]; // 1MB
        new Random().NextBytes(packageData);
        
        // Adicionar header MSI simulado
        packageData[0] = 0xD0;
        packageData[1] = 0xCF;
        packageData[2] = 0x11;
        packageData[3] = 0xE0;
        
        return packageData;
    }

    private async Task<byte[]> CreateFaultyUpdatePackageAsync(string version)
    {
        // Criar um pacote defeituoso que causará falha na instalação
        var packageData = new byte[1024 * 512]; // 512KB
        Array.Fill(packageData, (byte)0xFF); // Dados inválidos
        
        return packageData;
    }

    private async Task<byte[]> CreateCorruptedUpdatePackageAsync(string version)
    {
        var packageData = await CreateUpdatePackageAsync(version);
        
        // Corromper alguns bytes
        var random = new Random();
        for (int i = 0; i < 100; i++)
        {
            var index = random.Next(packageData.Length);
            packageData[index] = (byte)random.Next(256);
        }
        
        return packageData;
    }

    private async Task<Dictionary<string, object>> CreateUserDataAsync(Guid agentId)
    {
        return new Dictionary<string, object>
        {
            ["configuration"] = new { setting1 = "value1", setting2 = 42 },
            ["preferences"] = new { theme = "dark", language = "en" },
            ["customData"] = new { key1 = "data1", key2 = "data2" }
        };
    }

    private async Task SaveUserDataAsync(Guid agentId, Dictionary<string, object> userData)
    {
        var request = new { AgentId = agentId, UserData = userData };
        await _fixture.HttpClient.PostAsJsonAsync("/api/agents/save-user-data", request);
    }

    private async Task<Dictionary<string, object>> GetUserDataAsync(Guid agentId)
    {
        var response = await _fixture.HttpClient.GetAsync($"/api/agents/{agentId}/user-data");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        }
        return new Dictionary<string, object>();
    }

    private async Task SimulateNetworkFailureAsync()
    {
        await _fixture.HttpClient.PostAsync("/api/test/simulate-network-failure", null);
    }

    private async Task RestoreNetworkAsync()
    {
        await _fixture.HttpClient.PostAsync("/api/test/restore-network", null);
    }

    private async Task SimulateMaintenanceWindowAsync(Guid agentId, string version)
    {
        var request = new { AgentId = agentId, Version = version };
        await _fixture.HttpClient.PostAsJsonAsync("/api/test/simulate-maintenance-window", request);
    }
}

public class UpdateStatusDto
{
    public bool UpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public DateTime LastCheck { get; set; }
    public string UpdateDescription { get; set; } = string.Empty;
}

public class DownloadStatusDto
{
    public bool IsComplete { get; set; }
    public bool IsValid { get; set; }
    public bool ChecksumValid { get; set; }
    public long FileSize { get; set; }
    public int RetryCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? CompletionTime { get; set; }
}

public class InstallationStatusDto
{
    public bool IsComplete { get; set; }
    public bool IsSuccessful { get; set; }
    public string NewVersion { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? CompletionTime { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class RollbackStatusDto
{
    public bool RollbackExecuted { get; set; }
    public bool RollbackSuccessful { get; set; }
    public string RestoredVersion { get; set; } = string.Empty;
    public DateTime RollbackTime { get; set; }
    public string RollbackReason { get; set; } = string.Empty;
}

public class UpdateScheduleStatusDto
{
    public bool IsScheduled { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string MaintenanceWindow { get; set; } = string.Empty;
    public bool CanReschedule { get; set; }
}