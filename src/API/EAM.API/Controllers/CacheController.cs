using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Cache.Models;

namespace EAM.API.Controllers;

/// <summary>
/// Controller para operações de cache
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CacheController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CacheController> _logger;

    /// <summary>
    /// Construtor
    /// </summary>
    /// <param name="cacheService">Serviço de cache</param>
    /// <param name="logger">Logger</param>
    public CacheController(ICacheService cacheService, ILogger<CacheController> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém estatísticas do cache
    /// </summary>
    /// <returns>Estatísticas do cache</returns>
    [HttpGet("statistics")]
    public async Task<ActionResult<CacheStatistics>> GetStatistics()
    {
        try
        {
            var statistics = await _cacheService.GetStatisticsAsync();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas do cache");
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Verifica se uma chave existe no cache
    /// </summary>
    /// <param name="key">Chave para verificar</param>
    /// <returns>True se existe</returns>
    [HttpGet("exists/{key}")]
    public async Task<ActionResult<bool>> CheckExists(string key)
    {
        try
        {
            var exists = await _cacheService.ExistsAsync(key);
            return Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar existência da chave {Key}", key);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém valor do cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <returns>Valor do cache</returns>
    [HttpGet("get/{key}")]
    public async Task<ActionResult<object>> GetValue(string key)
    {
        try
        {
            var value = await _cacheService.GetAsync<object>(key);
            
            if (value == null)
            {
                return NotFound(new { message = "Chave não encontrada no cache" });
            }

            return Ok(new { key, value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter valor do cache para chave {Key}", key);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Define valor no cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="request">Dados para armazenar</param>
    /// <returns>Resultado da operação</returns>
    [HttpPost("set/{key}")]
    public async Task<ActionResult> SetValue(string key, [FromBody] SetCacheRequest request)
    {
        try
        {
            TimeSpan? expiration = null;
            if (request.ExpirationMinutes.HasValue)
            {
                expiration = TimeSpan.FromMinutes(request.ExpirationMinutes.Value);
            }

            var success = await _cacheService.SetAsync(key, request.Value, expiration);
            
            if (success)
            {
                return Ok(new { message = "Valor armazenado com sucesso", key });
            }
            else
            {
                return StatusCode(500, new { message = "Falha ao armazenar valor no cache" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao definir valor no cache para chave {Key}", key);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Remove valor do cache
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <returns>Resultado da operação</returns>
    [HttpDelete("remove/{key}")]
    public async Task<ActionResult> RemoveValue(string key)
    {
        try
        {
            var success = await _cacheService.RemoveAsync(key);
            
            if (success)
            {
                return Ok(new { message = "Valor removido com sucesso", key });
            }
            else
            {
                return StatusCode(500, new { message = "Falha ao remover valor do cache" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover valor do cache para chave {Key}", key);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Remove valores por padrão
    /// </summary>
    /// <param name="pattern">Padrão para buscar chaves</param>
    /// <returns>Número de chaves removidas</returns>
    [HttpDelete("remove-pattern/{pattern}")]
    public async Task<ActionResult> RemoveByPattern(string pattern)
    {
        try
        {
            var removedCount = await _cacheService.RemoveByPatternAsync(pattern);
            
            return Ok(new { message = "Valores removidos com sucesso", pattern, removedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover valores por padrão {Pattern}", pattern);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Obtém tempo de vida restante de uma chave
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <returns>Tempo de vida restante</returns>
    [HttpGet("ttl/{key}")]
    public async Task<ActionResult<object>> GetTimeToLive(string key)
    {
        try
        {
            var ttl = await _cacheService.GetTimeToLiveAsync(key);
            
            if (ttl.HasValue)
            {
                return Ok(new { key, ttl = ttl.Value.TotalSeconds });
            }
            else
            {
                return NotFound(new { message = "Chave não encontrada ou não tem expiração" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter TTL para chave {Key}", key);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Incrementa um valor numérico
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="value">Valor para incrementar (padrão: 1)</param>
    /// <returns>Novo valor</returns>
    [HttpPost("increment/{key}")]
    public async Task<ActionResult<object>> IncrementValue(string key, [FromQuery] long value = 1)
    {
        try
        {
            var newValue = await _cacheService.IncrementAsync(key, value);
            
            return Ok(new { key, newValue });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao incrementar valor para chave {Key}", key);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Decrementa um valor numérico
    /// </summary>
    /// <param name="key">Chave do cache</param>
    /// <param name="value">Valor para decrementar (padrão: 1)</param>
    /// <returns>Novo valor</returns>
    [HttpPost("decrement/{key}")]
    public async Task<ActionResult<object>> DecrementValue(string key, [FromQuery] long value = 1)
    {
        try
        {
            var newValue = await _cacheService.DecrementAsync(key, value);
            
            return Ok(new { key, newValue });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao decrementar valor para chave {Key}", key);
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }

    /// <summary>
    /// Limpa todo o cache (usar com cuidado!)
    /// </summary>
    /// <returns>Resultado da operação</returns>
    [HttpDelete("clear")]
    public async Task<ActionResult> ClearCache()
    {
        try
        {
            var success = await _cacheService.ClearAsync();
            
            if (success)
            {
                _logger.LogWarning("Cache completamente limpo pelo usuário");
                return Ok(new { message = "Cache limpo com sucesso" });
            }
            else
            {
                return StatusCode(500, new { message = "Falha ao limpar cache" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar cache");
            return StatusCode(500, new { message = "Erro interno do servidor" });
        }
    }
}

/// <summary>
/// Modelo para requisição de definição de cache
/// </summary>
public class SetCacheRequest
{
    /// <summary>
    /// Valor a ser armazenado
    /// </summary>
    public object Value { get; set; } = null!;

    /// <summary>
    /// Tempo de expiração em minutos (opcional)
    /// </summary>
    public int? ExpirationMinutes { get; set; }
}