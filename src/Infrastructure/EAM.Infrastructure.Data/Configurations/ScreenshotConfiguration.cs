using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EAM.API.Core.Entities;

namespace EAM.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração da entidade Screenshot
/// </summary>
public class ScreenshotConfiguration : IEntityTypeConfiguration<Screenshot>
{
    /// <summary>
    /// Configura a entidade Screenshot
    /// </summary>
    /// <param name="builder">Construtor da entidade</param>
    public void Configure(EntityTypeBuilder<Screenshot> builder)
    {
        // Nome da tabela
        builder.ToTable("screenshots");

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

        builder.Property(e => e.ActivityEventId)
            .HasColumnName("activity_event_id");

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.FilePath)
            .HasColumnName("file_path")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(e => e.BucketName)
            .HasColumnName("bucket_name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.ObjectKey)
            .HasColumnName("object_key")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(e => e.FileHash)
            .HasColumnName("file_hash")
            .HasMaxLength(32);

        builder.Property(e => e.FileSize)
            .HasColumnName("file_size")
            .IsRequired();

        builder.Property(e => e.Width)
            .HasColumnName("width")
            .IsRequired();

        builder.Property(e => e.Height)
            .HasColumnName("height")
            .IsRequired();

        builder.Property(e => e.Format)
            .HasColumnName("format")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Quality)
            .HasColumnName("quality")
            .IsRequired();

        builder.Property(e => e.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ForegroundApplication)
            .HasColumnName("foreground_application")
            .HasMaxLength(256);

        builder.Property(e => e.ForegroundWindowTitle)
            .HasColumnName("foreground_window_title")
            .HasMaxLength(512);

        builder.Property(e => e.ContainsSensitiveContent)
            .HasColumnName("contains_sensitive_content")
            .HasDefaultValue(false);

        builder.Property(e => e.IsProcessed)
            .HasColumnName("is_processed")
            .HasDefaultValue(false);

        builder.Property(e => e.IsUploaded)
            .HasColumnName("is_uploaded")
            .HasDefaultValue(false);

        builder.Property(e => e.UploadedAt)
            .HasColumnName("uploaded_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.PublicUrl)
            .HasColumnName("public_url")
            .HasMaxLength(1024);

        builder.Property(e => e.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .HasMaxLength(1024);

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(e => e.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasConversion<int>()
            .HasDefaultValue(ScreenshotProcessingStatus.Pending);

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(512);

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0);

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
            .HasDatabaseName("ix_screenshots_timestamp_brin")
            .HasMethod("brin");

        builder.HasIndex(e => new { e.AgentId, e.Timestamp })
            .HasDatabaseName("ix_screenshots_agent_timestamp");

        builder.HasIndex(e => new { e.UserId, e.Timestamp })
            .HasDatabaseName("ix_screenshots_user_timestamp");

        builder.HasIndex(e => e.ProcessingStatus)
            .HasDatabaseName("ix_screenshots_processing_status");

        builder.HasIndex(e => e.FileHash)
            .HasDatabaseName("ix_screenshots_file_hash");

        builder.HasIndex(e => e.ExpiresAt)
            .HasDatabaseName("ix_screenshots_expires_at");

        builder.HasIndex(e => e.IsUploaded)
            .HasDatabaseName("ix_screenshots_is_uploaded");

        builder.HasIndex(e => e.IsProcessed)
            .HasDatabaseName("ix_screenshots_is_processed");

        builder.HasIndex(e => e.ContainsSensitiveContent)
            .HasDatabaseName("ix_screenshots_contains_sensitive_content");

        builder.HasIndex(e => e.ObjectKey)
            .HasDatabaseName("ix_screenshots_object_key")
            .IsUnique();

        builder.HasIndex(e => new { e.BucketName, e.ObjectKey })
            .HasDatabaseName("ix_screenshots_bucket_object_key");

        builder.HasIndex(e => e.ForegroundApplication)
            .HasDatabaseName("ix_screenshots_foreground_application");

        // Índice composto para consultas de limpeza
        builder.HasIndex(e => new { e.ExpiresAt, e.ProcessingStatus })
            .HasDatabaseName("ix_screenshots_expires_status");

        // Relacionamentos
        builder.HasOne(e => e.Agent)
            .WithMany(e => e.Screenshots)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ActivityEvent)
            .WithOne(e => e.Screenshot)
            .HasForeignKey<Screenshot>(e => e.ActivityEventId)
            .OnDelete(DeleteBehavior.SetNull);

        // Comentários
        builder.HasComment("Tabela de screenshots particionada por data");

        // Configuração de particionamento (será implementada via SQL raw nas migrations)
        // O EF Core não suporta nativamente particionamento do PostgreSQL
    }
}