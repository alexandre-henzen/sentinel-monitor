using EAM.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EAM.API.Data;

public class EamDbContext : DbContext
{
    public EamDbContext(DbContextOptions<EamDbContext> options) : base(options)
    {
    }

    public DbSet<Agent> Agents { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<DailyScore> DailyScores { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurar esquema
        modelBuilder.HasDefaultSchema("eam");

        // Configurar Agent
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.ToTable("agents");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MachineId).IsUnique();
            entity.Property(e => e.MachineId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.MachineName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.UserName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        // Configurar ActivityLog
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("activity_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.EventTimestamp);
            entity.HasIndex(e => e.EventType);
            
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ApplicationName).HasMaxLength(255);
            entity.Property(e => e.WindowTitle).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(2000);
            entity.Property(e => e.ProcessName).HasMaxLength(255);
            entity.Property(e => e.ScreenshotPath).HasMaxLength(255);
            
            // Configurar metadata como JSON
            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null));

            // Configurar relacionamento com Agent
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.ActivityLogs)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configurar User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).HasMaxLength(50).HasDefaultValue("User");
        });

        // Configurar DailyScore
        modelBuilder.Entity<DailyScore>(entity =>
        {
            entity.ToTable("daily_scores");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AgentId, e.ActivityDate }).IsUnique();
            
            entity.Property(e => e.ActivityDate).HasColumnType("date");
            entity.Property(e => e.AvgProductivity).HasColumnType("decimal(5,2)");

            // Configurar relacionamento com Agent
            entity.HasOne(e => e.Agent)
                .WithMany(a => a.DailyScores)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configurar funções e triggers do PostgreSQL
        modelBuilder.HasPostgresExtension("uuid-ossp");
        
        // Seed data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed usuário admin
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@eam.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
    }
}