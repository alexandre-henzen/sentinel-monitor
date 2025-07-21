using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using EAM.Agent.Core.Data;
using EAM.Agent.Core.Services;
using EAM.Agent.Core.Models;

namespace EAM.Agent;

/// <summary>
/// Programa para testar apenas o SessionTracker isoladamente
/// </summary>
public class TestSessionTracker
{
    public static async Task TestAsync()
    {
        Console.WriteLine("=== TESTE SESSIONTRACKER ===");
        
        var services = new ServiceCollection();
        
        // Adicionar logging
        services.AddLogging(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        // Adicionar DbContext
        services.AddDbContext<AgentDbContext>(options =>
        {
            options.UseSqlite("Data Source=EAM.Agent.db");
            options.EnableSensitiveDataLogging();
        });
        
        // Configuração mock
        services.Configure<TrackerConfiguration>(config => 
        {
            config.Enabled = true;
            config.IntervalSeconds = 2;
        });
        
        // Adicionar SessionTracker
        services.AddTransient<SessionTracker>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<TestSessionTracker>>();
        
        try 
        {
            // Garantir que o banco existe
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                await dbContext.Database.EnsureCreatedAsync();
                logger.LogInformation("Banco de dados inicializado");
            }
            
            // Testar SessionTracker
            using (var scope = serviceProvider.CreateScope())
            {
                var sessionTracker = scope.ServiceProvider.GetRequiredService<SessionTracker>();
                logger.LogInformation("SessionTracker criado: {Name}", sessionTracker.Name);
                
                // Iniciar SessionTracker
                await sessionTracker.StartAsync();
                logger.LogInformation("SessionTracker iniciado com sucesso");
                
                // Aguardar 10 segundos para capturar atividade
                logger.LogInformation("Aguardando 10 segundos para capturar atividade...");
                await Task.Delay(10000);
                
                // Parar SessionTracker
                await sessionTracker.StopAsync();
                logger.LogInformation("SessionTracker parado");
                
                // Verificar dados coletados
                using var dbScope = serviceProvider.CreateScope();
                var dbContext = dbScope.ServiceProvider.GetRequiredService<AgentDbContext>();
                
                var sessionCount = await dbContext.SessionEvents.CountAsync();
                logger.LogInformation("Total de sessões coletadas: {Count}", sessionCount);
                
                if (sessionCount > 0)
                {
                    var sessions = await dbContext.SessionEvents
                        .OrderByDescending(s => s.StartTime)
                        .Take(5)
                        .ToListAsync();
                    
                    foreach (var session in sessions)
                    {
                        logger.LogInformation("Sessão: {App} - {Type} - {Duration}s - {Start} até {End}",
                            session.ApplicationName,
                            session.SessionType,
                            session.DurationSeconds,
                            session.StartTime,
                            session.EndTime?.ToString() ?? "ativa");
                    }
                }
            }
            
            Console.WriteLine("=== TESTE CONCLUÍDO ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro durante teste do SessionTracker");
        }
    }
}