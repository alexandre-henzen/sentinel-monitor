using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EAM.API.Core.Interfaces;
using EAM.Infrastructure.Data.Context;
using EAM.Infrastructure.Data.Repositories;

namespace EAM.Infrastructure.Data.Extensions;

/// <summary>
/// Extensões para configuração dos serviços de dados
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adiciona os serviços de dados ao container de DI
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Adicionar contexto do banco de dados
        services.AddDbContext<EamDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                
                npgsqlOptions.CommandTimeout(60);
                npgsqlOptions.MigrationsAssembly(typeof(EamDbContext).Assembly.FullName);
            });

            // Configurações adicionais para desenvolvimento
            if (configuration.GetValue<bool>("Database:EnableSensitiveDataLogging"))
            {
                options.EnableSensitiveDataLogging();
            }

            if (configuration.GetValue<bool>("Database:EnableDetailedErrors"))
            {
                options.EnableDetailedErrors();
            }
        });

        // Registrar repositórios
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IActivityEventRepository, ActivityEventRepository>();
        services.AddScoped<IScreenshotRepository, ScreenshotRepository>();

        // Configurar pool de conexões
        services.AddDbContextPool<EamDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString);
        }, poolSize: 128);

        return services;
    }

    /// <summary>
    /// Adiciona health checks para o banco de dados
    /// </summary>
    /// <param name="services">Coleção de serviços</param>
    /// <param name="configuration">Configuração</param>
    /// <returns>Coleção de serviços</returns>
    public static IServiceCollection AddDatabaseHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: new[] { "database", "postgresql" })
            .AddDbContextCheck<EamDbContext>(name: "eam-dbcontext", tags: new[] { "database", "efcore" });

        return services;
    }

    /// <summary>
    /// Executa migrações do banco de dados
    /// </summary>
    /// <param name="services">Provedor de serviços</param>
    /// <returns>Task</returns>
    public static async Task RunDatabaseMigrationsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EamDbContext>();
        
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Inicializa o banco de dados com dados seed
    /// </summary>
    /// <param name="services">Provedor de serviços</param>
    /// <returns>Task</returns>
    public static async Task SeedDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EamDbContext>();
        
        // Criar partições para o mês atual e próximo
        await CreatePartitionsAsync(context);
    }

    /// <summary>
    /// Cria partições para as tabelas particionadas
    /// </summary>
    /// <param name="context">Contexto do banco</param>
    /// <returns>Task</returns>
    private static async Task CreatePartitionsAsync(EamDbContext context)
    {
        var currentDate = DateTime.UtcNow;
        var nextMonth = currentDate.AddMonths(1);

        // Criar partições para activity_events
        await CreateMonthlyPartition(context, "activity_events", currentDate);
        await CreateMonthlyPartition(context, "activity_events", nextMonth);

        // Criar partições para screenshots
        await CreateMonthlyPartition(context, "screenshots", currentDate);
        await CreateMonthlyPartition(context, "screenshots", nextMonth);

        // Criar partições para scores_daily
        await CreateMonthlyPartition(context, "scores_daily", currentDate);
        await CreateMonthlyPartition(context, "scores_daily", nextMonth);
    }

    /// <summary>
    /// Cria partição mensal para uma tabela
    /// </summary>
    /// <param name="context">Contexto do banco</param>
    /// <param name="tableName">Nome da tabela</param>
    /// <param name="date">Data para a partição</param>
    /// <returns>Task</returns>
    private static async Task CreateMonthlyPartition(EamDbContext context, string tableName, DateTime date)
    {
        var year = date.Year;
        var month = date.Month;
        var partitionName = $"{tableName}_{year}_{month:D2}";
        
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var sql = $@"
            CREATE TABLE IF NOT EXISTS {partitionName} 
            PARTITION OF {tableName} 
            FOR VALUES FROM ('{startDate:yyyy-MM-dd}') TO ('{endDate:yyyy-MM-dd}');
        ";

        await context.Database.ExecuteSqlRawAsync(sql);
    }
}