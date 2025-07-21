namespace EAM.API.Core.Interfaces;

/// <summary>
/// Interface para serviço de cache Redis
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Obtém um valor do cache
    /// </summary>
    /// <typeparam name="T">Tipo do valor</typeparam>
    /// <param name="key">Chave do cache</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Valor do cache ou null se não encontrado</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Armazena um valor no cache
    /// </summary>
    /// <typeparam name="T">Tipo do valor</typeparam>
    /// <param name="key">Chave do cache</param>
    /// <param name="value">Valor a ser armazenado</param>
    /// <param name="expiry">Tempo de expiração</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se armazenado com sucesso</returns>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove um valor do cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido com sucesso</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se uma chave existe no cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se existe</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Define tempo de expiração para uma chave
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="expiry">Tempo de expiração</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se definido com sucesso</returns>
    Task<bool> ExpireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém múltiplos valores do cache
    /// </summary>
    /// <typeparam name="T">Tipo do valor</typeparam>
    /// <param name="keys">Lista de chaves</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Dicionário com os valores encontrados</returns>
    Task<Dictionary<string, T?>> GetMultipleAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Armazena múltiplos valores no cache
    /// </summary>
    /// <typeparam name="T">Tipo do valor</typeparam>
    /// <param name="keyValuePairs">Dicionário com chaves e valores</param>
    /// <param name="expiry">Tempo de expiração</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se todos foram armazenados com sucesso</returns>
    Task<bool> SetMultipleAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove múltiplas chaves do cache
    /// </summary>
    /// <param name="keys">Lista de chaves</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de chaves removidas</returns>
    Task<int> RemoveMultipleAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca chaves por padrão
    /// </summary>
    /// <param name="pattern">Padrão de busca (wildcards permitidos)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de chaves encontradas</returns>
    Task<IEnumerable<string>> SearchKeysAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Limpa todas as chaves que correspondem a um padrão
    /// </summary>
    /// <param name="pattern">Padrão de busca</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de chaves removidas</returns>
    Task<int> ClearByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementa um valor numérico no cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="increment">Valor a ser incrementado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Novo valor após incremento</returns>
    Task<long> IncrementAsync(string key, long increment = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrementa um valor numérico no cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="decrement">Valor a ser decrementado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Novo valor após decremento</returns>
    Task<long> DecrementAsync(string key, long decrement = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém informações sobre o cache
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações do cache</returns>
    Task<CacheInfo> GetCacheInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona item a uma lista
    /// </summary>
    /// <param name="key">Chave da lista</param>
    /// <param name="value">Valor a ser adicionado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Tamanho da lista após adição</returns>
    Task<long> ListPushAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove e retorna item de uma lista
    /// </summary>
    /// <param name="key">Chave da lista</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Item removido ou null se lista vazia</returns>
    Task<string?> ListPopAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém tamanho de uma lista
    /// </summary>
    /// <param name="key">Chave da lista</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Tamanho da lista</returns>
    Task<long> ListLengthAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona item a um conjunto (set)
    /// </summary>
    /// <param name="key">Chave do conjunto</param>
    /// <param name="value">Valor a ser adicionado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se adicionado (não existia), False se já existia</returns>
    Task<bool> SetAddAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove item de um conjunto
    /// </summary>
    /// <param name="key">Chave do conjunto</param>
    /// <param name="value">Valor a ser removido</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se removido, False se não existia</returns>
    Task<bool> SetRemoveAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se item existe em um conjunto
    /// </summary>
    /// <param name="key">Chave do conjunto</param>
    /// <param name="value">Valor a ser verificado</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se existe no conjunto</returns>
    Task<bool> SetContainsAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém todos os itens de um conjunto
    /// </summary>
    /// <param name="key">Chave do conjunto</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de itens do conjunto</returns>
    Task<IEnumerable<string>> SetMembersAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Informações sobre o cache
/// </summary>
public class CacheInfo
{
    /// <summary>
    /// Número total de chaves
    /// </summary>
    public long TotalKeys { get; set; }

    /// <summary>
    /// Memória usada em bytes
    /// </summary>
    public long UsedMemoryBytes { get; set; }

    /// <summary>
    /// Memória máxima em bytes
    /// </summary>
    public long MaxMemoryBytes { get; set; }

    /// <summary>
    /// Número de conexões ativas
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Estatísticas de hit/miss
    /// </summary>
    public CacheHitMissStats HitMissStats { get; set; } = new();

    /// <summary>
    /// Tempo de atividade
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Versão do Redis
    /// </summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Estatísticas de hit/miss do cache
/// </summary>
public class CacheHitMissStats
{
    /// <summary>
    /// Número de hits
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Número de misses
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Taxa de hit (0-1)
    /// </summary>
    public double HitRate => (Hits + Misses) > 0 ? (double)Hits / (Hits + Misses) : 0;

    /// <summary>
    /// Taxa de miss (0-1)
    /// </summary>
    public double MissRate => 1 - HitRate;
}