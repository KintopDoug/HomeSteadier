using HomeSteadier.Database;
using Microsoft.Extensions.Configuration;

namespace HomeSteadier.CLI.Services;

public class DatabaseService
{
    private readonly DatabaseMigrationService _migrationService;

    public DatabaseService()
    {
        _migrationService = new DatabaseMigrationService();
    }

    public async Task RunMigrationsAsync(IConfiguration configuration)
    {
        try
        {
            var connectionString = GetConnectionString(configuration);
            Console.WriteLine("Running database migrations...");

            var result = await _migrationService.RunMigrationsAsync(connectionString);

            if (!result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n{result.Message}: {result.Error}");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n{result.Message}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError running migrations: {ex.Message}");
            Console.ResetColor();
        }
    }

    private string GetConnectionString(IConfiguration configuration)
    {
        var host = configuration["Database:Host"] ?? "localhost";
        var port = configuration["Database:Port"] ?? "5432";
        var name = configuration["Database:Name"]
            ?? throw new InvalidOperationException("Database:Name not found in configuration.");
        var username = configuration["Database:Username"] ?? "postgres";
        var password = GetPostgresPassword();

        return $"Host={host};Port={port};Database={name};Username={username};Password={password}";
    }

    private string GetPostgresPassword()
    {
        return Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Machine)
            ?? throw new InvalidOperationException(
                "POSTGRES_PASSWORD environment variable is not set. Set it with: setx POSTGRES_PASSWORD \"<password>\" and restart your terminal/IDE.");
    }
}
