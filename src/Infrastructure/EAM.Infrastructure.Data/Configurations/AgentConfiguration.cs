using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EAM.API.Core.Entities;

namespace EAM.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração da entidade Agent
/// </summary>
public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    /// <summary>
    /// Configura a entidade Agent
    /// </summary>
    /// <param name="builder">Construtor da entidade</param>
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        // Nome da tabela
        builder.ToTable("agents");

        // Chave primária
        builder.HasKey(e => e.Id);

        // Propriedades
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.ComputerName)
            .HasColumnName("computer_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.DomainName)
            .HasColumnName("domain_name")
            .HasMaxLength(256);

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(e => e.MacAddress)
            .HasColumnName("mac_address")
            .HasMaxLength(17);

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.OsVersion)
            .HasColumnName("os_version")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.OsArchitecture)
            .HasColumnName("os_architecture")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.LastHeartbeat)
            .HasColumnName("last_heartbeat")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .HasDefaultValue(AgentStatus.Active);

        builder.Property(e => e.SystemInfo)
            .HasColumnName("system_info")
            .HasColumnType("jsonb");

        builder.Property(e => e.Configuration)
            .HasColumnName("configuration")
            .HasColumnType("jsonb");

        builder.Property(e => e.Certificate)
            .HasColumnName("certificate")
            .HasColumnType("text");

        builder.Property(e => e.LastTokenId)
            .HasColumnName("last_token_id");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Índices
        builder.HasIndex(e => new { e.ComputerName, e.UserName })
            .HasDatabaseName("ix_agents_computer_user")
            .IsUnique();

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_agents_status");

        builder.HasIndex(e => e.LastHeartbeat)
            .HasDatabaseName("ix_agents_last_heartbeat");

        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("ix_agents_is_active");

        builder.HasIndex(e => e.Version)
            .HasDatabaseName("ix_agents_version");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_agents_user_id");

        // Relacionamentos
        builder.HasMany(e => e.ActivityEvents)
            .WithOne(e => e.Agent)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Screenshots)
            .WithOne(e => e.Agent)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.DailyScores)
            .WithOne(e => e.Agent)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comentários
        builder.HasComment("Tabela de agentes de monitoramento");
    }
}