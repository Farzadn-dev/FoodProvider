namespace DbUpdater.Context
{
    public class PostgresConfiguration
    {
        /*
         *var PostgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
if (string.IsNullOrEmpty(PostgresHost))
{
    Console.WriteLine("POSTGRES_HOST environment variable is not set.");
    Environment.Exit(128);
}

var PostgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
var PostgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
var PostgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";
var PostgresDatabase = Environment.GetEnvironmentVariable("POSTGRES_DATABASE") ?? "postgres";
         */

        public required string PostgresHost { get; set; }
        public required string PostgresPort { get; set; }
        public required string PostgresUser { get; set; }
        public required string PostgresPassword { get; set; }
        public required string PostgresDatabase { get; set; }
    }
}