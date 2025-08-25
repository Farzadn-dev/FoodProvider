using DbUpdater.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DbUpdater.Context
{
    public class PostgresContext : DbContext
    {
        private readonly IConfiguration? _config;

        public PostgresContext(DbContextOptions<PostgresContext> options, IConfiguration? config = null)
            : base(options)
        {
            _config = config;
        }

        public DbSet<SearchRequest> SearchRequests { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && _config != null)
            {
                var connectionString = _config.GetConnectionString("Default");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    var host = _config["POSTGRES_HOST"] ?? "localhost";
                    var port = _config["POSTGRES_PORT"] ?? "5432";
                    var user = _config["POSTGRES_USER"] ?? "postgres";
                    var password = _config["POSTGRES_PASSWORD"] ?? "postgres";
                    var database = _config["POSTGRES_DATABASE"] ?? "postgres";

                    connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password}";
                }

                optionsBuilder.UseNpgsql(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SearchRequest>()
                .HasIndex(s => s.Id)
                .IsUnique();
        }
    }

    // This class is only used at design-time (Add-Migration, Update-Database)
    public class PostgresContextFactory : IDesignTimeDbContextFactory<PostgresContext>
    {
        public PostgresContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PostgresContext>();

            // Hard-coded design-time connection string (local development)
            var connectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

            optionsBuilder.UseNpgsql(connectionString);

            return new PostgresContext(optionsBuilder.Options);
        }
    }
}
