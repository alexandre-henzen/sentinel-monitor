using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Processing.Services;

namespace EAM.Infrastructure.Processing.Extensions;

/// <summary>
/// Extensões para configuração do container de injeção de dependência
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona os serviços de processamento assíncrono
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddAsyncProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configurar as configurações de processamento
        services.Configure<ProcessingSettings>(
            configuration.GetSection(ProcessingSettings.SectionName));

        // Registrar serviços de processamento
        services.AddSingleton<IEventIngestionService, EventIngestionService>();
        services.AddHostedService<EventProcessingService>();

        return services;
    }

    /// <summary>
    /// Adiciona os serviços de processamento assíncrono com configuração customizada
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configureOptions">Configuração customizada</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddAsyncProcessing(
        this IServiceCollection services,
        Action<ProcessingSettings> configureOptions)
    {
        // Configurar as configurações de processamento
        services.Configure(configureOptions);

        // Registrar serviços de processamento
        services.AddSingleton<IEventIngestionService, EventIngestionService>();
        services.AddHostedService<EventProcessingService>();

        return services;
    }

    /// <summary>
    /// Adiciona validação de configuração de processamento
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddProcessingValidation(this IServiceCollection services)
    {
        services.AddOptions<ProcessingSettings>()
            .Validate(settings => settings.ChannelCapacity > 0,
                "Channel capacity must be greater than 0")
            .Validate(settings => settings.MaxConcurrentBatches > 0,
                "Max concurrent batches must be greater than 0")
            .Validate(settings => settings.MaxBatchSize > 0,
                "Max batch size must be greater than 0")
            .Validate(settings => settings.DatabaseBatchSize > 0,
                "Database batch size must be greater than 0")
            .Validate(settings => settings.MaxRetryAttempts >= 0,
                "Max retry attempts must be greater than or equal to 0")
            .Validate(settings => settings.RetryDelaySeconds > 0,
                "Retry delay seconds must be greater than 0");

        return services;
    }

    /// <summary>
    /// Adiciona health checks para processamento
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="name">Nome do health check</param>
    /// <param name="tags">Tags para o health check</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddProcessingHealthChecks(
        this IServiceCollection services,
        string name = "processing",
        params string[] tags)
    {
        services.AddHealthChecks()
            .AddCheck<ProcessingHealthCheck>(name, tags: tags);

        return services;
    }

    /// <summary>
    /// Adiciona todos os serviços de processamento com configuração padrão
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddProcessingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddAsyncProcessing(configuration)
            .AddProcessingValidation()
            .AddProcessingHealthChecks();
    }

    /// <summary>
    /// Adiciona todos os serviços de processamento com configuração customizada
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configureOptions">Configuração customizada</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddProcessingServices(
        this IServiceCollection services,
        Action<ProcessingSettings> configureOptions)
    {
        return services
            .AddAsyncProcessing(configureOptions)
            .AddProcessingValidation()
            .AddProcessingHealthChecks();
    }

    /// <summary>
    /// Adiciona serviços de processamento para desenvolvimento
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddDevelopmentProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddProcessingServices(settings =>
        {
            settings.ChannelCapacity = 100;
            settings.MaxConcurrentBatches = 2;
            settings.MaxBatchSize = 50;
            settings.DatabaseBatchSize = 10;
            settings.MaxRetryAttempts = 2;
            settings.RetryDelaySeconds = 1;
        });
    }

    /// <summary>
    /// Adiciona serviços de processamento para produção
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddProductionProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddProcessingServices(settings =>
        {
            settings.ChannelCapacity = 10000;
            settings.MaxConcurrentBatches = 10;
            settings.MaxBatchSize = 1000;
            settings.DatabaseBatchSize = 100;
            settings.MaxRetryAttempts = 5;
            settings.RetryDelaySeconds = 5;
        });
    }

    /// <summary>
    /// Adiciona serviços de processamento com configuração baseada no ambiente
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <param name="environment">Ambiente atual</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddEnvironmentProcessing(
        this IServiceCollection services,
        IConfiguration configuration,
        string environment)
    {
        return environment.ToLowerInvariant() switch
        {
            "development" => services.AddDevelopmentProcessing(configuration),
            "production" => services.AddProductionProcessing(configuration),
            _ => services.AddProcessingServices(configuration)
        };
    }
}