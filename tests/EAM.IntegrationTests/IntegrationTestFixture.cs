using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.MinIO;
using Xunit;
using StackExchange.Redis;
using Minio;

namespace EAM.IntegrationTests;

/// <summary>
/// Fixture para testes de integração que gerencia infraestrutura Docker
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly TestConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly HttpClient _httpClient;
    private readonly ILogger<IntegrationTestFixture> _logger;

    // Containers Docker
    public PostgreSqlContainer PostgreSqlContainer { get; private set; }
    public RedisContainer RedisContainer { get; private set; }
    public MinioContainer MinioContainer { get; private set; }

    // Conexões
    public IConnectionMultiplexer RedisConnection { get; private set; }
    public IMinioClient MinioClient { get; private set; }

    // Processos
    private Process _apiProcess;
    private Process _agentProcess;
    private Process _frontendProcess;

    public HttpClient HttpClient => _httpClient;
    public TestConfiguration Configuration => _configuration;

    public IntegrationTestFixture()
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Integration.json", optional: false)
            .AddEnvironmentVariables();

        var config = configBuilder.Build();
        _configuration = new TestConfiguration();
        config.Bind(_configuration);

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        services.AddSingleton(_configuration);
        services.AddHttpClient();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<IntegrationTestFixture>>();
        _httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
        _httpClient.BaseAddress = new Uri(_configuration.TestEnvironment.ApiBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TestEnvironment.TestTimeoutSeconds);

        InitializeContainers();
    }

    private void InitializeContainers()
    {
        if (_configuration.TestEnvironment.UseTestContainers)
        {
            PostgreSqlContainer = new PostgreSqlBuilder()
                .WithImage("postgres:15")
                .WithDatabase("eam_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithPortBinding(5432, true)
                .Build();

            RedisContainer = new RedisBuilder()
                .WithImage("redis:7")
                .WithPortBinding(6379, true)
                .Build();

            MinioContainer = new MinioBuilder()
                .WithImage("minio/minio:latest")
                .WithPortBinding(9000, true)
                .WithPortBinding(9001, true)
                .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
                .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
                .Build();
        }
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Inicializando fixture de testes de integração...");
        
        if (_configuration.TestEnvironment.UseTestContainers)
        {
            await StartContainersAsync();
            await InitializeConnectionsAsync();
            await PrepareTestDataAsync();
        }
        
        _logger.LogInformation("Fixture de testes de integração inicializada com sucesso");
    }

    public async Task DisposeAsync()
    {
        _logger.LogInformation("Liberando recursos do fixture de testes de integração...");
        
        await StopAllProcessesAsync();
        
        if (_configuration.TestEnvironment.UseTestContainers)
        {
            await DisposeConnectionsAsync();
            await StopContainersAsync();
        }
        
        _httpClient?.Dispose();
        _serviceProvider?.Dispose();
        
        _logger.LogInformation("Recursos liberados com sucesso");
    }

    public async Task StartInfrastructureAsync()
    {
        _logger.LogInformation("Iniciando infraestrutura de testes...");
        
        if (_configuration.TestEnvironment.UseTestContainers)
        {
            await StartContainersAsync();
            await InitializeConnectionsAsync();
            await PrepareTestDataAsync();
        }
        
        _logger.LogInformation("Infraestrutura de testes iniciada com sucesso");
    }

    public async Task StopInfrastructureAsync()
    {
        _logger.LogInformation("Parando infraestrutura de testes...");
        
        if (_configuration.TestEnvironment.UseTestContainers)
        {
            await DisposeConnectionsAsync();
            await StopContainersAsync();
        }
        
        _logger.LogInformation("Infraestrutura de testes parada com sucesso");
    }

    public async Task StartApiAsync()
    {
        if (_apiProcess != null) return;
        
        _logger.LogInformation("Iniciando API de testes...");
        
        var apiPath = GetApiExecutablePath();
        if (File.Exists(apiPath))
        {
            _apiProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = apiPath,
                    Arguments = "--environment Integration",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(apiPath)
                }
            };

            _apiProcess.Start();
            
            // Aguardar API inicializar
            await WaitForApiHealthAsync();
            
            _logger.LogInformation("API de testes iniciada com sucesso");
        }
        else
        {
            _logger.LogWarning($"Executável da API não encontrado: {apiPath}");
        }
    }

    public async Task StopApiAsync()
    {
        if (_apiProcess != null)
        {
            _logger.LogInformation("Parando API de testes...");
            
            try
            {
                _apiProcess.Kill();
                await _apiProcess.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Erro ao parar API: {ex.Message}");
            }
            finally
            {
                _apiProcess?.Dispose();
                _apiProcess = null;
            }
            
            _logger.LogInformation("API de testes parada");
        }
    }

    public async Task StartAgentAsync()
    {
        if (_agentProcess != null) return;
        
        _logger.LogInformation("Iniciando Agente de testes...");
        
        var agentPath = GetAgentExecutablePath();
        if (File.Exists(agentPath))
        {
            _agentProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = agentPath,
                    Arguments = "--environment Integration",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(agentPath)
                }
            };

            _agentProcess.Start();
            
            // Aguardar agente inicializar
            await Task.Delay(5000);
            
            _logger.LogInformation("Agente de testes iniciado com sucesso");
        }
        else
        {
            _logger.LogWarning($"Executável do Agente não encontrado: {agentPath}");
        }
    }

    public async Task StopAgentAsync()
    {
        if (_agentProcess != null)
        {
            _logger.LogInformation("Parando Agente de testes...");
            
            try
            {
                _agentProcess.Kill();
                await _agentProcess.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Erro ao parar Agente: {ex.Message}");
            }
            finally
            {
                _agentProcess?.Dispose();
                _agentProcess = null;
            }
            
            _logger.LogInformation("Agente de testes parado");
        }
    }

    public async Task StartFrontendAsync()
    {
        if (_frontendProcess != null) return;
        
        _logger.LogInformation("Iniciando Frontend de testes...");
        
        var frontendPath = GetFrontendPath();
        if (Directory.Exists(frontendPath))
        {
            _frontendProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "start",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = frontendPath
                }
            };

            _frontendProcess.Start();
            
            // Aguardar frontend inicializar
            await Task.Delay(30000);
            
            _logger.LogInformation("Frontend de testes iniciado com sucesso");
        }
        else
        {
            _logger.LogWarning($"Diretório do Frontend não encontrado: {frontendPath}");
        }
    }

    public async Task StopFrontendAsync()
    {
        if (_frontendProcess != null)
        {
            _logger.LogInformation("Parando Frontend de testes...");
            
            try
            {
                _frontendProcess.Kill();
                await _frontendProcess.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Erro ao parar Frontend: {ex.Message}");
            }
            finally
            {
                _frontendProcess?.Dispose();
                _frontendProcess = null;
            }
            
            _logger.LogInformation("Frontend de testes parado");
        }
    }

    public ILogger<T> Logger<T>() => _serviceProvider.GetRequiredService<ILogger<T>>();

    private async Task StartContainersAsync()
    {
        _logger.LogInformation("Iniciando containers Docker...");
        
        var tasks = new List<Task>();
        
        if (PostgreSqlContainer != null)
        {
            tasks.Add(PostgreSqlContainer.StartAsync());
        }
        
        if (RedisContainer != null)
        {
            tasks.Add(RedisContainer.StartAsync());
        }
        
        if (MinioContainer != null)
        {
            tasks.Add(MinioContainer.StartAsync());
        }
        
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Containers Docker iniciados com sucesso");
    }

    private async Task StopContainersAsync()
    {
        _logger.LogInformation("Parando containers Docker...");
        
        var tasks = new List<Task>();
        
        if (PostgreSqlContainer != null)
        {
            tasks.Add(PostgreSqlContainer.StopAsync());
        }
        
        if (RedisContainer != null)
        {
            tasks.Add(RedisContainer.StopAsync());
        }
        
        if (MinioContainer != null)
        {
            tasks.Add(MinioContainer.StopAsync());
        }
        
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Containers Docker parados com sucesso");
    }

    private async Task InitializeConnectionsAsync()
    {
        _logger.LogInformation("Inicializando conexões com serviços...");
        
        // Inicializar Redis
        if (RedisContainer != null)
        {
            var redisConnectionString = RedisContainer.GetConnectionString();
            RedisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
        }
        
        // Inicializar MinIO
        if (MinioContainer != null)
        {
            var minioEndpoint = $"localhost:{MinioContainer.GetMappedPublicPort(9000)}";
            MinioClient = new MinioClient()
                .WithEndpoint(minioEndpoint)
                .WithCredentials("minioadmin", "minioadmin")
                .Build();
                
            // Criar buckets necessários
            await CreateMinioBucketsAsync();
        }
        
        _logger.LogInformation("Conexões inicializadas com sucesso");
    }

    private async Task DisposeConnectionsAsync()
    {
        _logger.LogInformation("Liberando conexões...");
        
        RedisConnection?.Dispose();
        
        _logger.LogInformation("Conexões liberadas");
    }

    private async Task CreateMinioBucketsAsync()
    {
        var buckets = new[] { "screenshots", "documents", "logs" };
        
        foreach (var bucket in buckets)
        {
            var bucketExistsArgs = new Minio.DataModel.Args.BucketExistsArgs().WithBucket(bucket);
            var bucketExists = await MinioClient.BucketExistsAsync(bucketExistsArgs);
            
            if (!bucketExists)
            {
                var makeBucketArgs = new Minio.DataModel.Args.MakeBucketArgs().WithBucket(bucket);
                await MinioClient.MakeBucketAsync(makeBucketArgs);
            }
        }
    }

    private async Task PrepareTestDataAsync()
    {
        _logger.LogInformation("Preparando dados de teste...");
        
        // Preparar dados de teste se necessário
        await Task.CompletedTask;
        
        _logger.LogInformation("Dados de teste preparados");
    }

    private async Task WaitForApiHealthAsync()
    {
        var maxAttempts = 30;
        var attempt = 0;
        
        while (attempt < maxAttempts)
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Ignorar erro e tentar novamente
            }
            
            attempt++;
            await Task.Delay(2000);
        }
        
        throw new TimeoutException("API não respondeu dentro do tempo limite");
    }

    private async Task StopAllProcessesAsync()
    {
        var tasks = new List<Task>
        {
            StopApiAsync(),
            StopAgentAsync(),
            StopFrontendAsync()
        };
        
        await Task.WhenAll(tasks);
    }

    private string GetApiExecutablePath()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(baseDir, "..", "..", "src", "EAM.API", "bin", "Debug", "net8.0", "EAM.API.exe");
        return Path.GetFullPath(apiPath);
    }

    private string GetAgentExecutablePath()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var agentPath = Path.Combine(baseDir, "..", "..", "src", "EAM.Agent", "bin", "Debug", "net8.0-windows", "EAM.Agent.exe");
        return Path.GetFullPath(agentPath);
    }

    private string GetFrontendPath()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var frontendPath = Path.Combine(baseDir, "..", "..", "src");
        return Path.GetFullPath(frontendPath);
    }
}

public class TestConfiguration
{
    public TestEnvironmentConfig TestEnvironment { get; set; } = new();
    public AgentConfig AgentConfig { get; set; } = new();
    public PerformanceThresholds PerformanceThresholds { get; set; } = new();
    public SecuritySettings SecuritySettings { get; set; } = new();
}

public class TestEnvironmentConfig
{
    public bool UseTestContainers { get; set; } = true;
    public string PostgreSqlConnectionString { get; set; } = string.Empty;
    public string RedisConnectionString { get; set; } = string.Empty;
    public string MinIOEndpoint { get; set; } = string.Empty;
    public string MinIOAccessKey { get; set; } = string.Empty;
    public string MinIOSecretKey { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = string.Empty;
    public int TestTimeoutSeconds { get; set; } = 300;
    public string AgentInstallPath { get; set; } = string.Empty;
    public string TestDataPath { get; set; } = string.Empty;
}

public class AgentConfig
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public int UpdateInterval { get; set; } = 30;
    public int ScreenshotInterval { get; set; } = 300;
    public int MaxRetries { get; set; } = 3;
    public bool OfflineMode { get; set; } = true;
    public bool TelemetryEnabled { get; set; } = true;
    public bool PluginsEnabled { get; set; } = true;
    public bool AutoUpdateEnabled { get; set; } = true;
}

public class PerformanceThresholds
{
    public double MaxCpuUsagePercent { get; set; } = 2.0;
    public int MaxMemoryUsageMB { get; set; } = 100;
    public int MaxEventsPerSecond { get; set; } = 10000;
    public int MaxResponseTimeMS { get; set; } = 1000;
    public double MinScreenshotSuccessRate { get; set; } = 99.5;
    public double MaxEventLossPercent { get; set; } = 0.1;
}

public class SecuritySettings
{
    public bool ValidateSSL { get; set; } = true;
    public bool RequireAuthentication { get; set; } = true;
    public int TokenExpirationMinutes { get; set; } = 60;
    public int MaxConcurrentConnections { get; set; } = 100;
}

/// <summary>
/// Coleção de testes que compartilham o mesmo fixture
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    // Esta classe existe apenas para definir a coleção
}