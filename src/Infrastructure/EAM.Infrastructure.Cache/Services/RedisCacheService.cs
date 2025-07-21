using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;

namespace EAM.Infrastructure.Cache.Services;

/// <summary>
/// Implementação do serviço de cache usando Redis
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Construtor do serviço
    /// </summary>
    /// <param name="distributedCache">Cache distribuído</param>
    /// <param name="connectionMultiplexer">Conexão Redis</param>
    /// <param name="cacheSettings">Configurações de cache</param>
    /// <param name="logger">Logger</param>
    public RedisCacheService(
        IDistributedCache distributedCache,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<CacheSettings> cacheSettings,
        ILogger<RedisCacheService> logger)
    {
        _distributedCache = distributedCache;
        _connectionMultiplexer = connectionMultiplexer;
        _database = connectionMultiplexer.GetDatabase();
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Obtém um valor do cache
    /// </summary>
    /// <typeparam name="T">Tipo do valor</typeparam>
    /// <param name="key">Chave do cache</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Valor do cache ou null se não encontrado</returns>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _distributedCache.GetStringAsync(key, cancellationToken);
            
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogDebug("Cache miss para chave: {Key}", key);
                return default;
            }

            var result = JsonSerializer.Deserialize<T>(value, _jsonOptions);
            _logger.LogDebug("Cache hit para chave: {Key}", key);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter valor do cache para chave: {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Define um valor no cache
    /// </summary>
    /// <typeparam name="T">Tipo do valor</typeparam>
    /// <param name="key">Chave do cache</param>
    /// <param name="value">Valor a ser armazenado</param>
    /// <param name="expiration">Tempo de expiração</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se armazenado com sucesso</returns>
    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
            
            var options = new DistributedCacheEntryOptions();
            
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                options.SlidingExpiration = TimeSpan.FromMinutes(_cacheSettings.DefaultExpirationMinutes);
            }

            await _distributedCache.SetStringAsync(key, serializedValue, options, cancellationToken);
            
            _logger.LogDebug("Valor armazenado no cache para chave: {Key} com expiração: {Expiration}", 
                key, expiration?.ToString() ?? "padrão");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao armazenar valor no cache para chave: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Remove um valor do cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
            
            _logger.LogDebug("Valor removido do cache para chave: {Key}", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover valor do cache para chave: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Verifica se uma chave existe no cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se existe</returns>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência da chave no cache: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Obtém ou define um valor no cache
    /// </summary>
    /// <typeparam name="T">Tipo do valor</typeparam>
    /// <param name="key">Chave do cache</param>
    /// <param name="factory">Função para gerar o valor se não estiver em cache</param>
    /// <param name="expiration">Tempo de expiração</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Valor do cache ou gerado pela factory</returns>
    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedValue = await GetAsync<T>(key, cancellationToken);
            
            if (cachedValue != null)
            {
                return cachedValue;
            }

            var value = await factory();
            await SetAsync(key, value, expiration, cancellationToken);
            
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na operação GetOrSet para chave: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Remove múltiplas chaves do cache
    /// </summary>
    /// <param name="keys">Lista de chaves</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de chaves removidas</returns>
    public async Task<int> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).ToArray();
            
            if (keys.Length == 0)
            {
                return 0;
            }

            var deletedCount = await _database.KeyDeleteAsync(keys);
            
            _logger.LogDebug("Removidas {Count} chaves do cache com padrão: {Pattern}", deletedCount, pattern);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover chaves do cache com padrão: {Pattern}", pattern);
            return 0;
        }
    }

    /// <summary>
    /// Obtém estatísticas do cache
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas do cache</returns>
    public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
            var info = await server.InfoAsync("memory");
            
            var statistics = new CacheStatistics();
            
            foreach (var item in info)
            {
                switch (item.Key)
                {
                    case "used_memory":
                        statistics.UsedMemoryBytes = long.Parse(item.Value);
                        break;
                    case "used_memory_human":
                        statistics.UsedMemoryFormatted = item.Value;
                        break;
                    case "total_system_memory":
                        statistics.TotalSystemMemoryBytes = long.Parse(item.Value);
                        break;
                    case "maxmemory":
                        statistics.MaxMemoryBytes = long.Parse(item.Value);
                        break;
                }
            }

            // Obter informações sobre keyspace
            var keyspaceInfo = await server.InfoAsync("keyspace");
            foreach (var item in keyspaceInfo)
            {
                if (item.Key.StartsWith("db"))
                {
                    var dbInfo = item.Value.Split(',');
                    foreach (var dbItem in dbInfo)
                    {
                        if (dbItem.StartsWith("keys="))
                        {
                            statistics.TotalKeys = int.Parse(dbItem.Substring(5));
                        }
                    }
                }
            }

            statistics.HitRate = await CalculateHitRateAsync();
            statistics.LastUpdated = DateTime.UtcNow;
            
            _logger.LogDebug("Estatísticas do cache obtidas: {TotalKeys} chaves, {UsedMemory} memória utilizada", 
                statistics.TotalKeys, statistics.UsedMemoryFormatted);
            
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas do cache");
            return new CacheStatistics();
        }
    }

    /// <summary>
    /// Define tempo de expiração para uma chave
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="expiration">Tempo de expiração</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se definido com sucesso</returns>
    public async Task<bool> SetExpirationAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.KeyExpireAsync(key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao definir expiração para chave: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Obtém tempo de vida restante de uma chave
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Tempo de vida restante ou null se não existe</returns>
    public async Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.KeyTimeToLiveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter TTL para chave: {Key}", key);
            return null;
        }
    }

    /// <summary>
    /// Incrementa um valor numérico no cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="value">Valor a ser incrementado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Novo valor após incremento</returns>
    public async Task<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.StringIncrementAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao incrementar valor para chave: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Decrementa um valor numérico no cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="value">Valor a ser decrementado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Novo valor após decremento</returns>
    public async Task<long> DecrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.StringDecrementAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao decrementar valor para chave: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Limpa todo o cache
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se limpeza foi bem-sucedida</returns>
    public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
            await server.FlushDatabaseAsync();
            
            _logger.LogWarning("Cache completamente limpo");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar cache");
            return false;
        }
    }

    /// <summary>
    /// Verifica se a conexão com Redis está ativa
    /// </summary>
    /// <returns>True se conectado</returns>
    public bool IsConnected => _connectionMultiplexer.IsConnected;

    /// <summary>
    /// Obtém informações sobre a conexão Redis
    /// </summary>
    /// <returns>Informações da conexão</returns>
    public string GetConnectionInfo()
    {
        var endpoints = _connectionMultiplexer.GetEndPoints();
        return string.Join(", ", endpoints.Select(e => e.ToString()));
    }

    /// <summary>
    /// Calcula a taxa de hit do cache
    /// </summary>
    /// <returns>Taxa de hit (0.0 a 1.0)</returns>
    private async Task<double> CalculateHitRateAsync()
    {
        try
        {
            var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
            var info = await server.InfoAsync("stats");
            
            long keyspaceHits = 0;
            long keyspaceMisses = 0;
            
            foreach (var item in info)
            {
                if (item.Key == "keyspace_hits")
                {
                    keyspaceHits = long.Parse(item.Value);
                }
                else if (item.Key == "keyspace_misses")
                {
                    keyspaceMisses = long.Parse(item.Value);
                }
            }

            var totalRequests = keyspaceHits + keyspaceMisses;
            return totalRequests > 0 ? (double)keyspaceHits / totalRequests : 0.0;
        }
        catch
        {
            return 0.0;
        }
    }
}