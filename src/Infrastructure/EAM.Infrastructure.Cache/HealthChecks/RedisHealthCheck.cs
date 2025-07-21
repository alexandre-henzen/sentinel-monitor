using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using EAM.API.Core.Configuration;

namespace EAM.Infrastructure.Cache.HealthChecks;

/// <summary>
/// Health check para Redis
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<RedisHealthCheck> _logger;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="connectionMultiplexer">Conexão Redis</param>
    /// <param name="cacheSettings">Configurações de cache</param>
    /// <param name="logger">Logger</param>
    public RedisHealthCheck(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<CacheSettings> cacheSettings,
        ILogger<RedisHealthCheck> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executa o health check
    /// </summary>
    /// <param name="context">Contexto do health check</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado do health check</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Verificar se a conexão está ativa
            if (!_connectionMultiplexer.IsConnected)
            {
                _logger.LogWarning("Redis health check: conexão não está ativa");
                
                return HealthCheckResult.Unhealthy(
                    "Redis connection is not active",
                    data: new Dictionary<string, object>
                    {
                        ["connection_state"] = "disconnected",
                        ["endpoints"] = string.Join(", ", _connectionMultiplexer.GetEndPoints().Select(e => e.ToString())),
                        ["check_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
                    });
            }

            var database = _connectionMultiplexer.GetDatabase();
            
            // Executar um comando PING para verificar a responsividade
            var pingResult = await database.PingAsync();
            var responseTime = DateTime.UtcNow - startTime;

            // Obter informações do servidor
            var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
            var serverInfo = await server.InfoAsync("server");
            
            // Obter informações de memória
            var memoryInfo = await server.InfoAsync("memory");
            
            // Obter informações de keyspace
            var keyspaceInfo = await server.InfoAsync("keyspace");

            var data = new Dictionary<string, object>
            {
                ["ping_response_ms"] = pingResult.TotalMilliseconds,
                ["total_response_ms"] = responseTime.TotalMilliseconds,
                ["connection_state"] = "connected",
                ["endpoints"] = string.Join(", ", _connectionMultiplexer.GetEndPoints().Select(e => e.ToString())),
                ["check_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };

            // Extrair informações do servidor
            foreach (var item in serverInfo)
            {
                switch (item.Key)
                {
                    case "redis_version":
                        data["redis_version"] = item.Value;
                        break;
                    case "redis_mode":
                        data["redis_mode"] = item.Value;
                        break;
                    case "uptime_in_seconds":
                        data["uptime_seconds"] = long.Parse(item.Value);
                        break;
                    case "connected_clients":
                        data["connected_clients"] = int.Parse(item.Value);
                        break;
                }
            }

            // Extrair informações de memória
            foreach (var item in memoryInfo)
            {
                switch (item.Key)
                {
                    case "used_memory":
                        data["used_memory_bytes"] = long.Parse(item.Value);
                        break;
                    case "used_memory_human":
                        data["used_memory_formatted"] = item.Value;
                        break;
                    case "used_memory_peak":
                        data["used_memory_peak_bytes"] = long.Parse(item.Value);
                        break;
                    case "maxmemory":
                        var maxMemory = long.Parse(item.Value);
                        if (maxMemory > 0)
                        {
                            data["max_memory_bytes"] = maxMemory;
                            var usedMemory = (long)data["used_memory_bytes"];
                            data["memory_usage_percentage"] = (double)usedMemory / maxMemory;
                        }
                        break;
                }
            }

            // Extrair informações de keyspace
            int totalKeys = 0;
            foreach (var item in keyspaceInfo)
            {
                if (item.Key.StartsWith("db"))
                {
                    var dbInfo = item.Value.Split(',');
                    foreach (var dbItem in dbInfo)
                    {
                        if (dbItem.StartsWith("keys="))
                        {
                            totalKeys += int.Parse(dbItem.Substring(5));
                        }
                    }
                }
            }
            data["total_keys"] = totalKeys;

            // Fazer um teste básico de leitura/escrita
            try
            {
                var testKey = $"health-check-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                var testValue = "health-check-test";
                
                // Teste de escrita
                await database.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
                
                // Teste de leitura
                var retrievedValue = await database.StringGetAsync(testKey);
                
                // Limpeza
                await database.KeyDeleteAsync(testKey);
                
                if (retrievedValue == testValue)
                {
                    data["read_write_test"] = "success";
                }
                else
                {
                    data["read_write_test"] = "failed";
                    _logger.LogWarning("Redis health check: teste de leitura/escrita falhou");
                    
                    return HealthCheckResult.Degraded(
                        "Redis is connected but read/write test failed",
                        data: data);
                }
            }
            catch (Exception ex)
            {
                data["read_write_test"] = "failed";
                data["read_write_error"] = ex.Message;
                
                _logger.LogWarning(ex, "Redis health check: erro no teste de leitura/escrita");
                
                return HealthCheckResult.Degraded(
                    $"Redis is connected but read/write test failed: {ex.Message}",
                    data: data);
            }

            // Verificar se a resposta está dentro do limite aceitável
            var responseTimeMs = responseTime.TotalMilliseconds;
            if (responseTimeMs > 1000) // 1 segundo
            {
                _logger.LogWarning("Redis health check: tempo de resposta alto ({ResponseTime}ms)", responseTimeMs);
                
                return HealthCheckResult.Degraded(
                    $"Redis is responding slowly ({responseTimeMs:F2}ms)",
                    data: data);
            }

            // Verificar uso de memória se configurado
            if (data.ContainsKey("memory_usage_percentage"))
            {
                var memoryUsage = (double)data["memory_usage_percentage"];
                if (memoryUsage > 0.95) // 95% de uso de memória
                {
                    _logger.LogWarning("Redis health check: uso de memória crítico ({MemoryUsage:P2})", memoryUsage);
                    
                    return HealthCheckResult.Degraded(
                        $"Redis memory usage is high ({memoryUsage:P2})",
                        data: data);
                }
            }

            _logger.LogDebug("Redis health check passou em todos os testes");
            
            return HealthCheckResult.Healthy(
                $"Redis is healthy (ping: {pingResult.TotalMilliseconds:F2}ms, total: {responseTime.TotalMilliseconds:F2}ms)",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check falhou");
            
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["error_type"] = ex.GetType().Name,
                ["connection_state"] = _connectionMultiplexer.IsConnected ? "connected" : "disconnected",
                ["endpoints"] = string.Join(", ", _connectionMultiplexer.GetEndPoints().Select(e => e.ToString())),
                ["check_time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
            };

            return HealthCheckResult.Unhealthy(
                $"Redis health check failed: {ex.Message}",
                exception: ex,
                data: errorData);
        }
    }
}