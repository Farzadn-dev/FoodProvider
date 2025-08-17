using DbUpdater.Domain;
using Microsoft.EntityFrameworkCore;

namespace DbUpdater.Context
{
    public class PostgresContext(PostgresConfiguration conf) : DbContext
    {
        public DbSet<SearchRequest> SearchRequests { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseNpgsql($"Host={conf.PostgresHost};Port={conf.PostgresPort};Database={conf.PostgresDatabase};Username={conf.PostgresUser};Password={conf.PostgresPassword}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SearchRequest>()
               .HasIndex(s => s.Id)
               .IsUnique();
        }
    }
}
