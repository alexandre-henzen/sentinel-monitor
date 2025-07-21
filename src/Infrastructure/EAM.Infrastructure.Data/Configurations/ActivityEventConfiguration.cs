using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EAM.API.Core.Entities;

namespace EAM.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração da entidade ActivityEvent
/// </summary>
public class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    /// <summary>
    /// Configura a entidade ActivityEvent
    /// </summary>
    /// <param name="builder">Construtor da entidade</param>
    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        // Nome da tabela
        builder.ToTable("activity_events");

        // Chave primária
        builder.HasKey(e => e.Id);

        // Propriedades
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.AgentId)
            .HasColumnName("agent_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ApplicationName)
            .HasColumnName("application_name")
            .HasMaxLength(256);

        builder.Property(e => e.WindowTitle)
            .HasColumnName("window_title")
            .HasMaxLength(512);

        builder.Property(e => e.Url)
            .HasColumnName("url")
            .HasMaxLength(2048);

        builder.Property(e => e.ProcessName)
            .HasColumnName("process_name")
            .HasMaxLength(256);

        builder.Property(e => e.ProcessId)
            .HasColumnName("process_id");

        builder.Property(e => e.DurationSeconds)
            .HasColumnName("duration_seconds");

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(false);

        builder.Property(e => e.ProductivityScore)
            .HasColumnName("productivity_score");

        builder.Property(e => e.ProductivityCategory)
            .HasColumnName("productivity_category")
            .HasMaxLength(64);

        builder.Property(e => e.ScreenshotPath)
            .HasColumnName("screenshot_path")
            .HasMaxLength(512);

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(e => e.EventHash)
            .HasColumnName("event_hash")
            .HasMaxLength(64);

        builder.Property(e => e.AgentVersion)
            .HasColumnName("agent_version")
            .HasMaxLength(32);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Índices BRIN para performance em dados temporais
        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("ix_activity_events_timestamp_brin")
            .HasMethod("brin");

        builder.HasIndex(e => new { e.AgentId, e.Timestamp })
            .HasDatabaseName("ix_activity_events_agent_timestamp");

        builder.HasIndex(e => new { e.UserId, e.Timestamp })
            .HasDatabaseName("ix_activity_events_user_timestamp");

        builder.HasIndex(e => e.EventType)
            .HasDatabaseName("ix_activity_events_event_type");

        builder.HasIndex(e => e.ApplicationName)
            .HasDatabaseName("ix_activity_events_application_name");

        builder.HasIndex(e => e.EventHash)
            .HasDatabaseName("ix_activity_events_event_hash");

        builder.HasIndex(e => e.ProductivityCategory)
            .HasDatabaseName("ix_activity_events_productivity_category");

        builder.HasIndex(e => e.ProductivityScore)
            .HasDatabaseName("ix_activity_events_productivity_score");

        builder.HasIndex(e => e.ProcessName)
            .HasDatabaseName("ix_activity_events_process_name");

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("ix_activity_events_is_active");

        // Índice composto para consultas frequentes
        builder.HasIndex(e => new { e.AgentId, e.EventType, e.Timestamp })
            .HasDatabaseName("ix_activity_events_agent_type_timestamp");

        // Índice para URLs
        builder.HasIndex(e => e.Url)
            .HasDatabaseName("ix_activity_events_url");

        // Relacionamentos
        builder.HasOne(e => e.Agent)
            .WithMany(e => e.ActivityEvents)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Screenshot)
            .WithOne(e => e.ActivityEvent)
            .HasForeignKey<Screenshot>(e => e.ActivityEventId)
            .OnDelete(DeleteBehavior.SetNull);

        // Comentários
        builder.HasComment("Tabela de eventos de atividade particionada por data");

        // Configuração de particionamento (será implementada via SQL raw nas migrations)
        // O EF Core não suporta nativamente particionamento do PostgreSQL
    }
}