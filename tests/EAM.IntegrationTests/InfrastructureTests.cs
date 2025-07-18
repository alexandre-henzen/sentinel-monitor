using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using StackExchange.Redis;
using Minio;
using Npgsql;

namespace EAM.IntegrationTests;

/// <summary>
/// Testes de validação da infraestrutura do EAM
/// Valida PostgreSQL, MinIO, Redis e conectividade
/// </summary>
[Collection("Integration")]
public class InfrastructureTests : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<InfrastructureTests> _logger;

    public InfrastructureTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _logger = _fixture.Logger<InfrastructureTests>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.StartInfrastructureAsync();
        await Task.Delay(5000);
    }

    public async Task DisposeAsync()
    {
        await _fixture.StopInfrastructureAsync();
    }

    [Fact]
    public async Task PostgreSQL_ShouldBeHealthyAndResponsive()
    {
        _output.WriteLine("Testando saúde do PostgreSQL...");

        // Act 1: Verificar conexão básica
        var connectionString = _fixture.PostgreSqlContainer.GetConnectionString();
        using var connection = new NpgsqlConnection(connectionString);
        
        await connection.OpenAsync();
        connection.State.Should().Be(System.Data.ConnectionState.Open);

        // Act 2: Executar query simples
        using var command = new NpgsqlCommand("SELECT version()", connection);
        var version = await command.ExecuteScalarAsync() as string;
        
        version.Should().NotBeNull();
        version.Should().Contain("PostgreSQL");

        // Act 3: Verificar performance de conexão
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            using var testCommand = new NpgsqlCommand("SELECT 1", connection);
            await testCommand.ExecuteScalarAsync();
        }
        var elapsed = DateTime.UtcNow - startTime;
        
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));

        // Act 4: Testar transações
        using var transaction = await connection.BeginTransactionAsync();
        using var txCommand = new NpgsqlCommand("CREATE TEMP TABLE test_table (id int)", connection, transaction);
        await txCommand.ExecuteNonQueryAsync();
        await transaction.CommitAsync();

        _output.WriteLine($"PostgreSQL está saudável: {version}");
    }

    [Fact]
    public async Task PostgreSQL_ShouldHandleHighConcurrency()
    {
        _output.WriteLine("Testando concorrência do PostgreSQL...");

        var connectionString = _fixture.PostgreSqlContainer.GetConnectionString();
        var concurrentTasks = new List<Task>();
        var successCount = 0;
        var lockObject = new object();

        // Act: Executar múltiplas conexões simultâneas
        for (int i = 0; i < 50; i++)
        {
            var taskIndex = i;
            concurrentTasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync();
                    
                    using var command = new NpgsqlCommand($"SELECT {taskIndex} as task_id", connection);
                    var result = await command.ExecuteScalarAsync();
                    
                    result.Should().Be(taskIndex);
                    
                    lock (lockObject)
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Erro na tarefa {taskIndex}: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(concurrentTasks);

        // Assert: Todas as conexões devem ter sucesso
        successCount.Should().Be(50);
        
        _output.WriteLine($"Concorrência testada: {successCount}/50 conexões bem-sucedidas");
    }

    [Fact]
    public async Task Redis_ShouldBeHealthyAndResponsive()
    {
        _output.WriteLine("Testando saúde do Redis...");

        // Act 1: Verificar conexão
        var database = _fixture.RedisConnection.GetDatabase();
        database.Should().NotBeNull();

        // Act 2: Testar operações básicas
        var testKey = "test:infrastructure:basic";
        var testValue = "test-value";
        
        await database.StringSetAsync(testKey, testValue);
        var retrievedValue = await database.StringGetAsync(testKey);
        
        retrievedValue.Should().Be(testValue);

        // Act 3: Testar operações avançadas
        var hashKey = "test:infrastructure:hash";
        var hashData = new HashEntry[]
        {
            new("field1", "value1"),
            new("field2", "value2"),
            new("field3", "value3")
        };
        
        await database.HashSetAsync(hashKey, hashData);
        var retrievedHash = await database.HashGetAllAsync(hashKey);
        
        retrievedHash.Should().HaveCount(3);
        retrievedHash.Should().Contain(h => h.Name == "field1" && h.Value == "value1");

        // Act 4: Testar performance
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < 1000; i++)
        {
            await database.StringSetAsync($"perf:test:{i}", $"value{i}");
        }
        var elapsed = DateTime.UtcNow - startTime;
        
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));

        // Cleanup
        await database.KeyDeleteAsync(testKey);
        await database.KeyDeleteAsync(hashKey);
        for (int i = 0; i < 1000; i++)
        {
            await database.KeyDeleteAsync($"perf:test:{i}");
        }

        _output.WriteLine($"Redis está saudável: {elapsed.TotalMilliseconds:F2}ms para 1000 operações");
    }

    [Fact]
    public async Task Redis_ShouldHandleExpiration()
    {
        _output.WriteLine("Testando expiração do Redis...");

        var database = _fixture.RedisConnection.GetDatabase();
        var testKey = "test:infrastructure:expiration";
        var testValue = "expiring-value";
        
        // Act 1: Definir chave com expiração
        await database.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(2));
        
        // Act 2: Verificar que existe
        var immediate = await database.StringGetAsync(testKey);
        immediate.Should().Be(testValue);

        // Act 3: Aguardar expiração
        await Task.Delay(3000);
        
        // Act 4: Verificar que expirou
        var expired = await database.StringGetAsync(testKey);
        expired.Should().BeNull();

        _output.WriteLine("Expiração do Redis funcionando corretamente");
    }

    [Fact]
    public async Task MinIO_ShouldBeHealthyAndResponsive()
    {
        _output.WriteLine("Testando saúde do MinIO...");

        // Act 1: Verificar conexão
        _fixture.MinioClient.Should().NotBeNull();

        // Act 2: Listar buckets
        var buckets = await _fixture.MinioClient.ListBucketsAsync();
        buckets.Should().NotBeNull();
        buckets.Buckets.Should().NotBeEmpty();

        // Act 3: Testar operações de objeto
        var testBucket = "test-bucket";
        var testObject = "test-object.txt";
        var testData = "This is test data for MinIO"u8.ToArray();

        // Criar bucket se não existir
        var bucketExists = await _fixture.MinioClient.BucketExistsAsync(
            new Minio.DataModel.Args.BucketExistsArgs().WithBucket(testBucket));
        
        if (!bucketExists)
        {
            await _fixture.MinioClient.MakeBucketAsync(
                new Minio.DataModel.Args.MakeBucketArgs().WithBucket(testBucket));
        }

        // Act 4: Upload de objeto
        using var stream = new MemoryStream(testData);
        await _fixture.MinioClient.PutObjectAsync(
            new Minio.DataModel.Args.PutObjectArgs()
                .WithBucket(testBucket)
                .WithObject(testObject)
                .WithStreamData(stream)
                .WithObjectSize(testData.Length));

        // Act 5: Verificar se objeto existe
        var objectExists = await ObjectExistsAsync(testBucket, testObject);
        objectExists.Should().BeTrue();

        // Act 6: Download de objeto
        var downloadedData = await DownloadObjectAsync(testBucket, testObject);
        downloadedData.Should().BeEquivalentTo(testData);

        // Act 7: Testar metadata
        var stat = await _fixture.MinioClient.StatObjectAsync(
            new Minio.DataModel.Args.StatObjectArgs()
                .WithBucket(testBucket)
                .WithObject(testObject));
        
        stat.Should().NotBeNull();
        stat.Size.Should().Be(testData.Length);

        // Cleanup
        await _fixture.MinioClient.RemoveObjectAsync(
            new Minio.DataModel.Args.RemoveObjectArgs()
                .WithBucket(testBucket)
                .WithObject(testObject));

        _output.WriteLine($"MinIO está saudável: objeto de {testData.Length} bytes processado");
    }

    [Fact]
    public async Task MinIO_ShouldHandleLargeFiles()
    {
        _output.WriteLine("Testando arquivos grandes no MinIO...");

        var testBucket = "large-files-test";
        var testObject = "large-file.bin";
        var fileSize = 10 * 1024 * 1024; // 10MB
        var testData = new byte[fileSize];
        new Random().NextBytes(testData);

        // Criar bucket se não existir
        var bucketExists = await _fixture.MinioClient.BucketExistsAsync(
            new Minio.DataModel.Args.BucketExistsArgs().WithBucket(testBucket));
        
        if (!bucketExists)
        {
            await _fixture.MinioClient.MakeBucketAsync(
                new Minio.DataModel.Args.MakeBucketArgs().WithBucket(testBucket));
        }

        // Act 1: Upload de arquivo grande
        var uploadStart = DateTime.UtcNow;
        using var stream = new MemoryStream(testData);
        await _fixture.MinioClient.PutObjectAsync(
            new Minio.DataModel.Args.PutObjectArgs()
                .WithBucket(testBucket)
                .WithObject(testObject)
                .WithStreamData(stream)
                .WithObjectSize(testData.Length));
        
        var uploadTime = DateTime.UtcNow - uploadStart;

        // Act 2: Verificar integridade
        var stat = await _fixture.MinioClient.StatObjectAsync(
            new Minio.DataModel.Args.StatObjectArgs()
                .WithBucket(testBucket)
                .WithObject(testObject));
        
        stat.Size.Should().Be(fileSize);

        // Act 3: Download parcial
        var downloadStart = DateTime.UtcNow;
        var partialData = await DownloadObjectRangeAsync(testBucket, testObject, 0, 1024);
        var downloadTime = DateTime.UtcNow - downloadStart;
        
        partialData.Should().HaveCount(1024);
        partialData.Should().BeEquivalentTo(testData.Take(1024));

        // Cleanup
        await _fixture.MinioClient.RemoveObjectAsync(
            new Minio.DataModel.Args.RemoveObjectArgs()
                .WithBucket(testBucket)
                .WithObject(testObject));

        _output.WriteLine($"Arquivo grande processado: {fileSize / 1024 / 1024}MB em {uploadTime.TotalSeconds:F2}s upload");
    }

    [Fact]
    public async Task NetworkConnectivity_ShouldBeStable()
    {
        _output.WriteLine("Testando conectividade de rede...");

        var connectivityTests = new Dictionary<string, Func<Task<bool>>>
        {
            ["PostgreSQL"] = async () => await TestPostgreSQLConnectivity(),
            ["Redis"] = async () => await TestRedisConnectivity(),
            ["MinIO"] = async () => await TestMinIOConnectivity(),
            ["API"] = async () => await TestAPIConnectivity()
        };

        var results = new Dictionary<string, bool>();
        var responseTimes = new Dictionary<string, TimeSpan>();

        foreach (var test in connectivityTests)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                results[test.Key] = await test.Value();
                responseTimes[test.Key] = DateTime.UtcNow - startTime;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Erro no teste {test.Key}: {ex.Message}");
                results[test.Key] = false;
                responseTimes[test.Key] = DateTime.UtcNow - startTime;
            }
        }

        // Assert: Todos os serviços devem estar acessíveis
        foreach (var result in results)
        {
            result.Value.Should().BeTrue($"{result.Key} deve estar acessível");
        }

        // Assert: Tempos de resposta aceitáveis
        foreach (var responseTime in responseTimes)
        {
            responseTime.Value.Should().BeLessThan(TimeSpan.FromSeconds(10), 
                $"{responseTime.Key} deve responder em menos de 10 segundos");
        }

        _output.WriteLine("Conectividade de rede validada:");
        foreach (var time in responseTimes)
        {
            _output.WriteLine($"  {time.Key}: {time.Value.TotalMilliseconds:F2}ms");
        }
    }

    [Fact]
    public async Task InfrastructureRecovery_ShouldHandleFailures()
    {
        _output.WriteLine("Testando recuperação da infraestrutura...");

        // Act 1: Verificar estado inicial
        var initialHealth = await CheckInfrastructureHealthAsync();
        initialHealth.Should().BeTrue();

        // Act 2: Simular falha do Redis
        _output.WriteLine("Simulando falha do Redis...");
        await _fixture.RedisContainer.StopAsync();
        await Task.Delay(5000);

        // Act 3: Verificar que sistema detectou falha
        var redisHealth = await TestRedisConnectivity();
        redisHealth.Should().BeFalse();

        // Act 4: Recuperar Redis
        _output.WriteLine("Recuperando Redis...");
        await _fixture.RedisContainer.StartAsync();
        await Task.Delay(10000); // Aguarda inicialização

        // Act 5: Verificar recuperação
        var recoveredHealth = await TestRedisConnectivity();
        recoveredHealth.Should().BeTrue();

        // Act 6: Simular falha do PostgreSQL
        _output.WriteLine("Simulando falha do PostgreSQL...");
        await _fixture.PostgreSqlContainer.StopAsync();
        await Task.Delay(5000);

        var pgHealth = await TestPostgreSQLConnectivity();
        pgHealth.Should().BeFalse();

        // Act 7: Recuperar PostgreSQL
        _output.WriteLine("Recuperando PostgreSQL...");
        await _fixture.PostgreSqlContainer.StartAsync();
        await Task.Delay(15000); // PostgreSQL demora mais para inicializar

        var pgRecoveredHealth = await TestPostgreSQLConnectivity();
        pgRecoveredHealth.Should().BeTrue();

        _output.WriteLine("Recuperação da infraestrutura validada com sucesso");
    }

    [Fact]
    public async Task InfrastructurePerformance_ShouldMeetBaselines()
    {
        _output.WriteLine("Testando performance da infraestrutura...");

        var performanceTests = new Dictionary<string, Func<Task<double>>>
        {
            ["PostgreSQL Query"] = async () => await MeasurePostgreSQLPerformance(),
            ["Redis Operations"] = async () => await MeasureRedisPerformance(),
            ["MinIO Upload"] = async () => await MeasureMinIOPerformance(),
            ["Network Latency"] = async () => await MeasureNetworkLatency()
        };

        var performanceBaselines = new Dictionary<string, double>
        {
            ["PostgreSQL Query"] = 100.0, // ms
            ["Redis Operations"] = 50.0,  // ms
            ["MinIO Upload"] = 1000.0,    // ms
            ["Network Latency"] = 10.0    // ms
        };

        var results = new Dictionary<string, double>();

        foreach (var test in performanceTests)
        {
            try
            {
                results[test.Key] = await test.Value();
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Erro no teste de performance {test.Key}: {ex.Message}");
                results[test.Key] = double.MaxValue;
            }
        }

        // Assert: Performance deve estar dentro dos baselines
        foreach (var result in results)
        {
            if (performanceBaselines.ContainsKey(result.Key))
            {
                result.Value.Should().BeLessThan(performanceBaselines[result.Key], 
                    $"{result.Key} deve ser mais rápido que {performanceBaselines[result.Key]}ms");
            }
        }

        _output.WriteLine("Performance da infraestrutura:");
        foreach (var result in results)
        {
            _output.WriteLine($"  {result.Key}: {result.Value:F2}ms");
        }
    }

    // Helper methods
    private async Task<bool> ObjectExistsAsync(string bucketName, string objectName)
    {
        try
        {
            await _fixture.MinioClient.StatObjectAsync(
                new Minio.DataModel.Args.StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<byte[]> DownloadObjectAsync(string bucketName, string objectName)
    {
        using var stream = new MemoryStream();
        await _fixture.MinioClient.GetObjectAsync(
            new Minio.DataModel.Args.GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(s => s.CopyTo(stream)));
        
        return stream.ToArray();
    }

    private async Task<byte[]> DownloadObjectRangeAsync(string bucketName, string objectName, long offset, int length)
    {
        using var stream = new MemoryStream();
        await _fixture.MinioClient.GetObjectAsync(
            new Minio.DataModel.Args.GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithOffsetAndLength(offset, length)
                .WithCallbackStream(s => s.CopyTo(stream)));
        
        return stream.ToArray();
    }

    private async Task<bool> TestPostgreSQLConnectivity()
    {
        try
        {
            var connectionString = _fixture.PostgreSqlContainer.GetConnectionString();
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync();
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TestRedisConnectivity()
    {
        try
        {
            var database = _fixture.RedisConnection.GetDatabase();
            await database.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TestMinIOConnectivity()
    {
        try
        {
            await _fixture.MinioClient.ListBucketsAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TestAPIConnectivity()
    {
        try
        {
            var response = await _fixture.HttpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckInfrastructureHealthAsync()
    {
        var healthChecks = new[]
        {
            TestPostgreSQLConnectivity(),
            TestRedisConnectivity(),
            TestMinIOConnectivity()
        };

        var results = await Task.WhenAll(healthChecks);
        return results.All(r => r);
    }

    private async Task<double> MeasurePostgreSQLPerformance()
    {
        var connectionString = _fixture.PostgreSqlContainer.GetConnectionString();
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 100; i++)
        {
            using var command = new NpgsqlCommand("SELECT $1", connection);
            command.Parameters.AddWithValue(i);
            await command.ExecuteScalarAsync();
        }
        
        var elapsed = DateTime.UtcNow - startTime;
        return elapsed.TotalMilliseconds;
    }

    private async Task<double> MeasureRedisPerformance()
    {
        var database = _fixture.RedisConnection.GetDatabase();
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 1000; i++)
        {
            await database.StringSetAsync($"perf:{i}", $"value{i}");
        }
        
        var elapsed = DateTime.UtcNow - startTime;
        
        // Cleanup
        for (int i = 0; i < 1000; i++)
        {
            await database.KeyDeleteAsync($"perf:{i}");
        }
        
        return elapsed.TotalMilliseconds;
    }

    private async Task<double> MeasureMinIOPerformance()
    {
        var testBucket = "performance-test";
        var testObject = "perf-test.bin";
        var testData = new byte[1024]; // 1KB
        new Random().NextBytes(testData);

        // Criar bucket se não existir
        var bucketExists = await _fixture.MinioClient.BucketExistsAsync(
            new Minio.DataModel.Args.BucketExistsArgs().WithBucket(testBucket));
        
        if (!bucketExists)
        {
            await _fixture.MinioClient.MakeBucketAsync(
                new Minio.DataModel.Args.MakeBucketArgs().WithBucket(testBucket));
        }

        var startTime = DateTime.UtcNow;
        
        using var stream = new MemoryStream(testData);
        await _fixture.MinioClient.PutObjectAsync(
            new Minio.DataModel.Args.PutObjectArgs()
                .WithBucket(testBucket)
                .WithObject(testObject)
                .WithStreamData(stream)
                .WithObjectSize(testData.Length));
        
        var elapsed = DateTime.UtcNow - startTime;
        
        // Cleanup
        await _fixture.MinioClient.RemoveObjectAsync(
            new Minio.DataModel.Args.RemoveObjectArgs()
                .WithBucket(testBucket)
                .WithObject(testObject));
        
        return elapsed.TotalMilliseconds;
    }

    private async Task<double> MeasureNetworkLatency()
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var response = await _fixture.HttpClient.GetAsync("/health");
            var elapsed = DateTime.UtcNow - startTime;
            
            return response.IsSuccessStatusCode ? elapsed.TotalMilliseconds : double.MaxValue;
        }
        catch
        {
            return double.MaxValue;
        }
    }
}