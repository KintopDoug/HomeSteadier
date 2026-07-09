using HomeSteadier.Database;
using Microsoft.Extensions.Configuration;

namespace HomeSteadier.CLI.Services;

public class DatabaseService
{
    private readonly DatabaseMigrationService _migrationService;
    private readonly TableScriptGenerationService _tableScriptGenerationService;

    public DatabaseService()
    {
        _migrationService = new DatabaseMigrationService();
        _tableScriptGenerationService = new TableScriptGenerationService();
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

    public async Task GenerateTableScriptsAsync(IConfiguration configuration)
    {
        try
        {
            var connectionString = GetConnectionString(configuration);
            var outputDirectory = GetTablesPath();

            Console.WriteLine("Generating table creation scripts from database schema...");
            Console.WriteLine($"Writing scripts to: {outputDirectory}");
            Console.WriteLine();

            var result = await _tableScriptGenerationService.GenerateTableScriptsAsync(connectionString, outputDirectory);

            if (!result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n{result.Message}: {result.Error}");
                Console.ResetColor();
                return;
            }

            foreach (var table in result.Created)
                Console.WriteLine($"Created: {table}.sql");

            foreach (var table in result.Updated)
                Console.WriteLine($"Updated: {table}.sql");

            foreach (var table in result.Removed)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Removed: {table}.sql (table no longer exists)");
                Console.ResetColor();
            }

            foreach (var table in result.Skipped)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Skipped: {table} (DbUp-managed table)");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nSuccessfully generated table scripts! ({result.Created.Count} created, {result.Updated.Count} updated, {result.Unchanged.Count} unchanged)");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError generating table scripts: {ex.Message}");
            Console.ResetColor();
        }
    }

    private string GetTablesPath()
    {
        var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
        return Path.Combine(solutionRoot, "HomeSteadier.Database", "Tables");
    }

    private string FindSolutionRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HomeSteadier.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return startPath;
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
