using DbUp;
using System.Reflection;

namespace HomeSteadier.Database;

public class DatabaseMigrationService
{
    public async Task<MigrationResult> RunMigrationsAsync(string connectionString)
    {
        try
        {
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrationService).Assembly)
                .WithTransaction()
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();

            return new MigrationResult
            {
                Success = result.Successful,
                Error = result.Error?.Message,
                Message = result.Successful ? "Migrations completed successfully!" : "Migration failed"
            };
        }
        catch (Exception ex)
        {
            return new MigrationResult
            {
                Success = false,
                Error = ex.Message,
                Message = "Error running migrations"
            };
        }
    }
}

public class MigrationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Message { get; set; } = string.Empty;
}
