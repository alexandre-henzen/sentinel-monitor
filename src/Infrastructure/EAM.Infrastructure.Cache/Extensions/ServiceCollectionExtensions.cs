using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Cache.Services;
using EAM.Infrastructure.Cache.HealthChecks;

namespace EAM.Infrastructure.Cache.Extensions;

/// <summary>
/// Extensões para configuração do container de injeção de dependência
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona os serviços de cache Redis
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configurar as configurações de cache
        services.Configure<CacheSettings>(
            configuration.GetSection(CacheSettings.SectionName));

        // Obter string de conexão Redis
        var redisConnectionString = configuration.GetConnectionString("RedisConnection")
            ?? throw new InvalidOperationException("Redis connection string não encontrada");

        // Registrar o multiplexer do Redis
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var cacheSettings = serviceProvider.GetRequiredService<IOptions<CacheSettings>>().Value;
            
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            
            // Configurar opções de conexão
            configuration.ConnectTimeout = cacheSettings.ConnectionTimeoutSeconds * 1000;
            configuration.SyncTimeout = cacheSettings.RequestTimeoutSeconds * 1000;
            configuration.AbortOnConnectFail = false;
            configuration.ReconnectRetryPolicy = new ExponentialRetry(1000, 10000);
            
            return ConnectionMultiplexer.Connect(configuration);
        });

        // Registrar o cache distribuído
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "EAM";
        });

        // Registrar o serviço de cache
        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Adiciona os serviços de cache Redis com configuração customizada
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="redisConnectionString">String de conexão Redis</param>
    /// <param name="configureOptions">Configuração customizada</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        string redisConnectionString,
        Action<CacheSettings> configureOptions)
    {
        // Configurar as configurações de cache
        services.Configure(configureOptions);

        // Registrar o multiplexer do Redis
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var cacheSettings = serviceProvider.GetRequiredService<IOptions<CacheSettings>>().Value;
            
            var configuration = ConfigurationOptions.Parse(redisConnectionString);
            
            // Configurar opções de conexão
            configuration.ConnectTimeout = cacheSettings.ConnectionTimeoutSeconds * 1000;
            configuration.SyncTimeout = cacheSettings.RequestTimeoutSeconds * 1000;
            configuration.AbortOnConnectFail = false;
            configuration.ReconnectRetryPolicy = new ExponentialRetry(1000, 10000);
            
            return ConnectionMultiplexer.Connect(configuration);
        });

        // Registrar o cache distribuído
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "EAM";
        });

        // Registrar o serviço de cache
        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Adiciona os serviços de cache Redis com multiplexer customizado
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <param name="configureMultiplexer">Configuração customizada do multiplexer</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ConfigurationOptions> configureMultiplexer)
    {
        // Configurar as configurações de cache
        services.Configure<CacheSettings>(
            configuration.GetSection(CacheSettings.SectionName));

        // Obter string de conexão Redis
        var redisConnectionString = configuration.GetConnectionString("RedisConnection")
            ?? throw new InvalidOperationException("Redis connection string não encontrada");

        // Registrar o multiplexer do Redis
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var cacheSettings = serviceProvider.GetRequiredService<IOptions<CacheSettings>>().Value;
            
            var configOptions = ConfigurationOptions.Parse(redisConnectionString);
            
            // Configurar opções padrão
            configOptions.ConnectTimeout = cacheSettings.ConnectionTimeoutSeconds * 1000;
            configOptions.SyncTimeout = cacheSettings.RequestTimeoutSeconds * 1000;
            configOptions.AbortOnConnectFail = false;
            configOptions.ReconnectRetryPolicy = new ExponentialRetry(1000, 10000);
            
            // Aplicar configuração customizada
            configureMultiplexer(configOptions);
            
            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Registrar o cache distribuído
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "EAM";
        });

        // Registrar o serviço de cache
        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Adiciona validação de configuração de cache
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddCacheValidation(this IServiceCollection services)
    {
        services.AddOptions<CacheSettings>()
            .Validate(settings => settings.DefaultExpirationMinutes > 0,
                "Default expiration minutes must be greater than 0")
            .Validate(settings => settings.SlidingExpirationMinutes > 0,
                "Sliding expiration minutes must be greater than 0")
            .Validate(settings => settings.ConnectionTimeoutSeconds > 0,
                "Connection timeout seconds must be greater than 0")
            .Validate(settings => settings.RequestTimeoutSeconds > 0,
                "Request timeout seconds must be greater than 0")
            .Validate(settings => settings.MaxRetryAttempts >= 0,
                "Max retry attempts must be greater than or equal to 0");

        return services;
    }

    /// <summary>
    /// Adiciona health checks para Redis
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="name">Nome do health check</param>
    /// <param name="tags">Tags para o health check</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddRedisHealthChecks(
        this IServiceCollection services,
        string name = "redis",
        params string[] tags)
    {
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>(name, tags: tags);

        return services;
    }

    /// <summary>
    /// Adiciona todos os serviços de cache com configuração padrão
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddCacheServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddRedisCache(configuration)
            .AddCacheValidation()
            .AddRedisHealthChecks();
    }

    /// <summary>
    /// Adiciona todos os serviços de cache com configuração customizada
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="redisConnectionString">String de conexão Redis</param>
    /// <param name="configureOptions">Configuração customizada</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddCacheServices(
        this IServiceCollection services,
        string redisConnectionString,
        Action<CacheSettings> configureOptions)
    {
        return services
            .AddRedisCache(redisConnectionString, configureOptions)
            .AddCacheValidation()
            .AddRedisHealthChecks();
    }

    /// <summary>
    /// Adiciona middleware para warming up do cache
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddCacheWarmup(this IServiceCollection services)
    {
        services.AddHostedService<CacheWarmupService>();
        return services;
    }

    /// <summary>
    /// Adiciona serviços de limpeza automática do cache
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddCacheCleanup(this IServiceCollection services)
    {
        services.AddHostedService<CacheCleanupService>();
        return services;
    }

    /// <summary>
    /// Adiciona todos os serviços de cache com recursos avançados
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <param name="enableWarmup">Habilitar aquecimento do cache</param>
    /// <param name="enableCleanup">Habilitar limpeza automática</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddAdvancedCacheServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableWarmup = true,
        bool enableCleanup = true)
    {
        services.AddCacheServices(configuration);

        if (enableWarmup)
            services.AddCacheWarmup();

        if (enableCleanup)
            services.AddCacheCleanup();

        return services;
    }
}