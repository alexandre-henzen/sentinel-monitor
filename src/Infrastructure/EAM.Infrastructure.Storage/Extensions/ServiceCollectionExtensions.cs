using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using EAM.API.Core.Configuration;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Storage.Services;

namespace EAM.Infrastructure.Storage.Extensions;

/// <summary>
/// Extensões para configuração do container de injeção de dependência
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona os serviços de armazenamento MinIO
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddMinIOStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configurar as configurações de storage
        services.Configure<StorageSettings>(
            configuration.GetSection(StorageSettings.SectionName));

        // Registrar o cliente MinIO
        services.AddSingleton<IMinioClient>(serviceProvider =>
        {
            var storageSettings = serviceProvider.GetRequiredService<IOptions<StorageSettings>>().Value;
            
            var minioClient = new MinioClient()
                .WithEndpoint(storageSettings.Endpoint)
                .WithCredentials(storageSettings.AccessKey, storageSettings.SecretKey);

            // Configurar HTTPS se necessário
            if (storageSettings.UseHttps)
            {
                minioClient = minioClient.WithSSL();
            }

            return minioClient.Build();
        });

        // Registrar o serviço de storage
        services.AddScoped<IStorageService, MinIOStorageService>();

        return services;
    }

    /// <summary>
    /// Adiciona os serviços de armazenamento MinIO com configuração customizada
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configureOptions">Configuração customizada</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddMinIOStorage(
        this IServiceCollection services,
        Action<StorageSettings> configureOptions)
    {
        // Configurar as configurações de storage
        services.Configure(configureOptions);

        // Registrar o cliente MinIO
        services.AddSingleton<IMinioClient>(serviceProvider =>
        {
            var storageSettings = serviceProvider.GetRequiredService<IOptions<StorageSettings>>().Value;
            
            var minioClient = new MinioClient()
                .WithEndpoint(storageSettings.Endpoint)
                .WithCredentials(storageSettings.AccessKey, storageSettings.SecretKey);

            // Configurar HTTPS se necessário
            if (storageSettings.UseHttps)
            {
                minioClient = minioClient.WithSSL();
            }

            return minioClient.Build();
        });

        // Registrar o serviço de storage
        services.AddScoped<IStorageService, MinIOStorageService>();

        return services;
    }

    /// <summary>
    /// Adiciona os serviços de armazenamento MinIO com cliente customizado
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <param name="configureClient">Configuração customizada do cliente</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddMinIOStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IMinioClient, StorageSettings> configureClient)
    {
        // Configurar as configurações de storage
        services.Configure<StorageSettings>(
            configuration.GetSection(StorageSettings.SectionName));

        // Registrar o cliente MinIO
        services.AddSingleton<IMinioClient>(serviceProvider =>
        {
            var storageSettings = serviceProvider.GetRequiredService<IOptions<StorageSettings>>().Value;
            
            var minioClient = new MinioClient()
                .WithEndpoint(storageSettings.Endpoint)
                .WithCredentials(storageSettings.AccessKey, storageSettings.SecretKey);

            // Configurar HTTPS se necessário
            if (storageSettings.UseHttps)
            {
                minioClient = minioClient.WithSSL();
            }

            var client = minioClient.Build();
            
            // Aplicar configuração customizada
            configureClient(client, storageSettings);

            return client;
        });

        // Registrar o serviço de storage
        services.AddScoped<IStorageService, MinIOStorageService>();

        return services;
    }

    /// <summary>
    /// Adiciona validação de configuração de storage
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddStorageValidation(this IServiceCollection services)
    {
        services.AddOptions<StorageSettings>()
            .Validate(settings => !string.IsNullOrEmpty(settings.Endpoint), 
                "Storage endpoint is required")
            .Validate(settings => !string.IsNullOrEmpty(settings.AccessKey), 
                "Storage access key is required")
            .Validate(settings => !string.IsNullOrEmpty(settings.SecretKey), 
                "Storage secret key is required")
            .Validate(settings => !string.IsNullOrEmpty(settings.DefaultBucket), 
                "Default bucket name is required")
            .Validate(settings => settings.UrlExpiryMinutes > 0, 
                "URL expiry minutes must be greater than 0")
            .Validate(settings => settings.MaxUploadSizeBytes > 0, 
                "Max upload size must be greater than 0")
            .Validate(settings => settings.DefaultTtlDays > 0, 
                "Default TTL days must be greater than 0");

        return services;
    }

    /// <summary>
    /// Adiciona health checks para MinIO
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="name">Nome do health check</param>
    /// <param name="tags">Tags para o health check</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddMinIOHealthChecks(
        this IServiceCollection services,
        string name = "minio",
        params string[] tags)
    {
        services.AddHealthChecks()
            .AddCheck<MinIOHealthCheck>(name, tags: tags);

        return services;
    }

    /// <summary>
    /// Adiciona todos os serviços de storage com configuração padrão
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração da aplicação</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddMinIOStorage(configuration)
            .AddStorageValidation()
            .AddMinIOHealthChecks();
    }

    /// <summary>
    /// Adiciona todos os serviços de storage com configuração customizada
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configureOptions">Configuração customizada</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        Action<StorageSettings> configureOptions)
    {
        return services
            .AddMinIOStorage(configureOptions)
            .AddStorageValidation()
            .AddMinIOHealthChecks();
    }
}