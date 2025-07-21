using Microsoft.EntityFrameworkCore;
using EAM.API.Core.Entities;
using EAM.Infrastructure.Data.Configurations;

namespace EAM.Infrastructure.Data.Context;

/// <summary>
/// Contexto do banco de dados para o EAM
/// </summary>
public class EamDbContext : DbContext
{
    /// <summary>
    /// Construtor do contexto
    /// </summary>
    /// <param name="options">Opções do contexto</param>
    public EamDbContext(DbContextOptions<EamDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Agentes
    /// </summary>
    public DbSet<Agent> Agents { get; set; } = null!;

    /// <summary>
    /// Eventos de atividade
    /// </summary>
    public DbSet<ActivityEvent> ActivityEvents { get; set; } = null!;

    /// <summary>
    /// Screenshots
    /// </summary>
    public DbSet<Screenshot> Screenshots { get; set; } = null!;

    /// <summary>
    /// Scores diários
    /// </summary>
    public DbSet<DailyScore> DailyScores { get; set; } = null!;

    /// <summary>
    /// Configura o modelo do banco de dados
    /// </summary>
    /// <param name="modelBuilder">Construtor do modelo</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar configurações das entidades
        modelBuilder.ApplyConfiguration(new AgentConfiguration());
        modelBuilder.ApplyConfiguration(new ActivityEventConfiguration());
        modelBuilder.ApplyConfiguration(new ScreenshotConfiguration());
        modelBuilder.ApplyConfiguration(new DailyScoreConfiguration());

        // Configurar particionamento para activity_events
        ConfigurePartitioning(modelBuilder);

        // Configurar índices para performance
        ConfigureIndexes(modelBuilder);

        // Configurar views materializadas
        ConfigureMaterializedViews(modelBuilder);
    }

    /// <summary>
    /// Configura particionamento de tabelas
    /// </summary>
    /// <param name="modelBuilder">Construtor do modelo</param>
    private void ConfigurePartitioning(ModelBuilder modelBuilder)
    {
        // Particionamento por data para activity_events
        modelBuilder.Entity<ActivityEvent>()
            .ToTable("activity_events", t => t.HasComment("Tabela particionada por data"));

        // Particionamento por data para screenshots
        modelBuilder.Entity<Screenshot>()
            .ToTable("screenshots", t => t.HasComment("Tabela particionada por data"));

        // Particionamento por data para scores_daily
        modelBuilder.Entity<DailyScore>()
            .ToTable("scores_daily", t => t.HasComment("Tabela particionada por data"));
    }

    /// <summary>
    /// Configura índices para performance
    /// </summary>
    /// <param name="modelBuilder">Construtor do modelo</param>
    private void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        // Índices para ActivityEvent
        modelBuilder.Entity<ActivityEvent>()
            .HasIndex(e => e.Timestamp)
            .HasDatabaseName("ix_activity_events_timestamp_brin")
            .HasMethod("brin");

        modelBuilder.Entity<ActivityEvent>()
            .HasIndex(e => new { e.AgentId, e.Timestamp })
            .HasDatabaseName("ix_activity_events_agent_timestamp");

        modelBuilder.Entity<ActivityEvent>()
            .HasIndex(e => new { e.UserId, e.Timestamp })
            .HasDatabaseName("ix_activity_events_user_timestamp");

        modelBuilder.Entity<ActivityEvent>()
            .HasIndex(e => e.EventType)
            .HasDatabaseName("ix_activity_events_event_type");

        modelBuilder.Entity<ActivityEvent>()
            .HasIndex(e => e.ApplicationName)
            .HasDatabaseName("ix_activity_events_application_name");

        modelBuilder.Entity<ActivityEvent>()
            .HasIndex(e => e.EventHash)
            .HasDatabaseName("ix_activity_events_event_hash");

        // Índices para Screenshot
        modelBuilder.Entity<Screenshot>()
            .HasIndex(e => e.Timestamp)
            .HasDatabaseName("ix_screenshots_timestamp_brin")
            .HasMethod("brin");

        modelBuilder.Entity<Screenshot>()
            .HasIndex(e => new { e.AgentId, e.Timestamp })
            .HasDatabaseName("ix_screenshots_agent_timestamp");

        modelBuilder.Entity<Screenshot>()
            .HasIndex(e => e.ProcessingStatus)
            .HasDatabaseName("ix_screenshots_processing_status");

        modelBuilder.Entity<Screenshot>()
            .HasIndex(e => e.FileHash)
            .HasDatabaseName("ix_screenshots_file_hash");

        modelBuilder.Entity<Screenshot>()
            .HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_screenshots_expires_at");

        // Índices para Agent
        modelBuilder.Entity<Agent>()
            .HasIndex(e => new { e.ComputerName, e.UserName })
            .HasDatabaseName("ix_agents_computer_user")
            .IsUnique();

        modelBuilder.Entity<Agent>()
            .HasIndex(e => e.Status)
            .HasDatabaseName("ix_agents_status");

        modelBuilder.Entity<Agent>()
            .HasIndex(e => e.LastHeartbeat)
            .HasDatabaseName("ix_agents_last_heartbeat");

        // Índices para DailyScore
        modelBuilder.Entity<DailyScore>()
            .HasIndex(e => e.ScoreDate)
            .HasDatabaseName("ix_scores_daily_score_date_brin")
            .HasMethod("brin");

        modelBuilder.Entity<DailyScore>()
            .HasIndex(e => new { e.AgentId, e.ScoreDate })
            .HasDatabaseName("ix_scores_daily_agent_date")
            .IsUnique();

        modelBuilder.Entity<DailyScore>()
            .HasIndex(e => new { e.UserId, e.ScoreDate })
            .HasDatabaseName("ix_scores_daily_user_date");
    }

    /// <summary>
    /// Configura views materializadas
    /// </summary>
    /// <param name="modelBuilder">Construtor do modelo</param>
    private void ConfigureMaterializedViews(ModelBuilder modelBuilder)
    {
        // View materializada para estatísticas diárias será criada via migration
        // devido à complexidade da configuração no EF Core
    }

    /// <summary>
    /// Configura conexão com PostgreSQL
    /// </summary>
    /// <param name="optionsBuilder">Construtor de opções</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Configuração padrão para desenvolvimento
            optionsBuilder.UseNpgsql("Host=localhost;Database=EAM_DB;Username=eam_user;Password=eam_password");
        }

        // Configurações específicas do PostgreSQL
        optionsBuilder.UseNpgsql(options =>
        {
            options.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });

        // Configurações de desenvolvimento
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
        }
    }

    /// <summary>
    /// Salva alterações com auditoria automática
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Número de entidades salvas</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auditoria automática
        var now = DateTime.UtcNow;
        
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is Agent agent)
                    {
                        agent.CreatedAt = now;
                        agent.UpdatedAt = now;
                    }
                    else if (entry.Entity is ActivityEvent activityEvent)
                    {
                        activityEvent.CreatedAt = now;
                        activityEvent.UpdatedAt = now;
                    }
                    else if (entry.Entity is Screenshot screenshot)
                    {
                        screenshot.CreatedAt = now;
                        screenshot.UpdatedAt = now;
                    }
                    else if (entry.Entity is DailyScore dailyScore)
                    {
                        dailyScore.CreatedAt = now;
                        dailyScore.UpdatedAt = now;
                    }
                    break;

                case EntityState.Modified:
                    if (entry.Entity is Agent modifiedAgent)
                    {
                        modifiedAgent.UpdatedAt = now;
                    }
                    else if (entry.Entity is ActivityEvent modifiedActivityEvent)
                    {
                        modifiedActivityEvent.UpdatedAt = now;
                    }
                    else if (entry.Entity is Screenshot modifiedScreenshot)
                    {
                        modifiedScreenshot.UpdatedAt = now;
                    }
                    else if (entry.Entity is DailyScore modifiedDailyScore)
                    {
                        modifiedDailyScore.UpdatedAt = now;
                    }
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}