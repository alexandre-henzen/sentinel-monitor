using EAM.Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace EAM.Agent.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ActivityEvent> ActivityEvents { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = Path.Combine(AppContext.BaseDirectory, "eam_local.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }
    }
}