using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EAM.API.Core.Entities;

namespace EAM.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração da entidade DailyScore
/// </summary>
public class DailyScoreConfiguration : IEntityTypeConfiguration<DailyScore>
{
    /// <summary>
    /// Configura a entidade DailyScore
    /// </summary>
    /// <param name="builder">Construtor da entidade</param>
    public void Configure(EntityTypeBuilder<DailyScore> builder)
    {
        // Nome da tabela
        builder.ToTable("scores_daily");

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

        builder.Property(e => e.ScoreDate)
            .HasColumnName("score_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.AverageProductivityScore)
            .HasColumnName("average_productivity_score")
            .HasColumnType("decimal(5,2)");

        builder.Property(e => e.MaxProductivityScore)
            .HasColumnName("max_productivity_score");

        builder.Property(e => e.MinProductivityScore)
            .HasColumnName("min_productivity_score");

        builder.Property(e => e.TotalEvents)
            .HasColumnName("total_events")
            .HasDefaultValue(0);

        builder.Property(e => e.TotalActiveTimeSeconds)
            .HasColumnName("total_active_time_seconds")
            .HasDefaultValue(0);

        builder.Property(e => e.TotalIdleTimeSeconds)
            .HasColumnName("total_idle_time_seconds")
            .HasDefaultValue(0);

        builder.Property(e => e.UniqueApplications)
            .HasColumnName("unique_applications")
            .HasDefaultValue(0);

        builder.Property(e => e.UniqueUrls)
            .HasColumnName("unique_urls")
            .HasDefaultValue(0);

        builder.Property(e => e.ScreenshotsCount)
            .HasColumnName("screenshots_count")
            .HasDefaultValue(0);

        builder.Property(e => e.FirstActivityTime)
            .HasColumnName("first_activity_time")
            .HasColumnType("time");

        builder.Property(e => e.LastActivityTime)
            .HasColumnName("last_activity_time")
            .HasColumnType("time");

        builder.Property(e => e.EffectiveWorkHours)
            .HasColumnName("effective_work_hours")
            .HasColumnType("decimal(5,2)");

        builder.Property(e => e.TimeDistribution)
            .HasColumnName("time_distribution")
            .HasColumnType("jsonb");

        builder.Property(e => e.TopApplications)
            .HasColumnName("top_applications")
            .HasColumnType("jsonb");

        builder.Property(e => e.TopUrls)
            .HasColumnName("top_urls")
            .HasColumnType("jsonb");

        builder.Property(e => e.ProductivityByCategory)
            .HasColumnName("productivity_by_category")
            .HasColumnType("jsonb");

        builder.Property(e => e.HourlyActivityPattern)
            .HasColumnName("hourly_activity_pattern")
            .HasColumnType("jsonb");

        builder.Property(e => e.TeamsMeetingsInfo)
            .HasColumnName("teams_meetings_info")
            .HasColumnType("jsonb");

        builder.Property(e => e.DataQualityScore)
            .HasColumnName("data_quality_score")
            .HasColumnType("decimal(5,2)");

        builder.Property(e => e.DataGapsCount)
            .HasColumnName("data_gaps_count")
            .HasDefaultValue(0);

        builder.Property(e => e.IsComplete)
            .HasColumnName("is_complete")
            .HasDefaultValue(false);

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1024);

        builder.Property(e => e.Tags)
            .HasColumnName("tags")
            .HasColumnType("text[]");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.LastCalculatedAt)
            .HasColumnName("last_calculated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Índices BRIN para performance em dados temporais
        builder.HasIndex(e => e.ScoreDate)
            .HasDatabaseName("ix_scores_daily_score_date_brin")
            .HasMethod("brin");

        builder.HasIndex(e => new { e.AgentId, e.ScoreDate })
            .HasDatabaseName("ix_scores_daily_agent_date")
            .IsUnique();

        builder.HasIndex(e => new { e.UserId, e.ScoreDate })
            .HasDatabaseName("ix_scores_daily_user_date");

        builder.HasIndex(e => e.AverageProductivityScore)
            .HasDatabaseName("ix_scores_daily_avg_productivity_score");

        builder.HasIndex(e => e.TotalEvents)
            .HasDatabaseName("ix_scores_daily_total_events");

        builder.HasIndex(e => e.TotalActiveTimeSeconds)
            .HasDatabaseName("ix_scores_daily_total_active_time");

        builder.HasIndex(e => e.EffectiveWorkHours)
            .HasDatabaseName("ix_scores_daily_effective_work_hours");

        builder.HasIndex(e => e.IsComplete)
            .HasDatabaseName("ix_scores_daily_is_complete");

        builder.HasIndex(e => e.LastCalculatedAt)
            .HasDatabaseName("ix_scores_daily_last_calculated_at");

        builder.HasIndex(e => e.DataQualityScore)
            .HasDatabaseName("ix_scores_daily_data_quality_score");

        // Índice composto para consultas de relatórios
        builder.HasIndex(e => new { e.UserId, e.ScoreDate, e.AverageProductivityScore })
            .HasDatabaseName("ix_scores_daily_user_date_score");

        // Índice GIN para arrays de tags
        builder.HasIndex(e => e.Tags)
            .HasDatabaseName("ix_scores_daily_tags_gin")
            .HasMethod("gin");

        // Relacionamentos
        builder.HasOne(e => e.Agent)
            .WithMany(e => e.DailyScores)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comentários
        builder.HasComment("Tabela de scores diários agregados particionada por data");

        // Constraints
        builder.HasCheckConstraint("ck_scores_daily_avg_productivity_score", 
            "average_productivity_score >= 0 AND average_productivity_score <= 100");

        builder.HasCheckConstraint("ck_scores_daily_max_productivity_score", 
            "max_productivity_score >= 0 AND max_productivity_score <= 100");

        builder.HasCheckConstraint("ck_scores_daily_min_productivity_score", 
            "min_productivity_score >= 0 AND min_productivity_score <= 100");

        builder.HasCheckConstraint("ck_scores_daily_data_quality_score", 
            "data_quality_score >= 0 AND data_quality_score <= 100");

        builder.HasCheckConstraint("ck_scores_daily_effective_work_hours", 
            "effective_work_hours >= 0 AND effective_work_hours <= 24");

        builder.HasCheckConstraint("ck_scores_daily_total_events", 
            "total_events >= 0");

        builder.HasCheckConstraint("ck_scores_daily_total_active_time", 
            "total_active_time_seconds >= 0");

        builder.HasCheckConstraint("ck_scores_daily_total_idle_time", 
            "total_idle_time_seconds >= 0");

        builder.HasCheckConstraint("ck_scores_daily_unique_applications", 
            "unique_applications >= 0");

        builder.HasCheckConstraint("ck_scores_daily_unique_urls", 
            "unique_urls >= 0");

        builder.HasCheckConstraint("ck_scores_daily_screenshots_count", 
            "screenshots_count >= 0");

        builder.HasCheckConstraint("ck_scores_daily_data_gaps_count", 
            "data_gaps_count >= 0");

        // Configuração de particionamento (será implementada via SQL raw nas migrations)
        // O EF Core não suporta nativamente particionamento do PostgreSQL
    }
}