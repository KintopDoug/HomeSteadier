using DbUp;
using Microsoft.Extensions.Configuration;
using System.Reflection;

var sharedConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.shared.json");
var localConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

var configuration = new ConfigurationBuilder()
    .AddJsonFile(sharedConfigPath, optional: false)
    .AddJsonFile(localConfigPath, optional: true)
    .AddEnvironmentVariables()
    .Build();

Console.WriteLine("HomeSteadier CLI");
Console.WriteLine("Type 'help' for available commands, 'exit' to quit.");
Console.WriteLine();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input))
        continue;

    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    switch (parts)
    {
        case ["exit"] or ["quit"]:
            Console.WriteLine("Goodbye!");
            return 0;

        case ["help"]:
            PrintHelp();
            break;

        case ["database", "update"]:
            await RunMigrations(configuration);
            break;

        default:
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Unknown command: '{input}'. Type 'help' for available commands.");
            Console.ResetColor();
            break;
    }

    Console.WriteLine();
}

void PrintHelp()
{
    Console.WriteLine("Available commands:");
    Console.WriteLine("  database update    Run pending migrations");
    Console.WriteLine("  help               Show this help message");
    Console.WriteLine("  exit               Exit the CLI");
}

async Task RunMigrations(IConfiguration configuration)
{
    try
    {
        var connectionString = GetConnectionString(configuration);

        Console.WriteLine("Running database migrations...");

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithTransaction()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nMigration failed: {result.Error}");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nMigrations completed successfully!");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\nError running migrations: {ex.Message}");
        Console.ResetColor();
    }
}

string GetConnectionString(IConfiguration configuration)
{
    var host = configuration["Database:Host"] ?? "localhost";
    var port = configuration["Database:Port"] ?? "5432";
    var name = configuration["Database:Name"]
        ?? throw new InvalidOperationException("Database:Name not found in configuration.");
    var username = configuration["Database:Username"] ?? "postgres";
    var password = GetPostgresPassword();

    return $"Host={host};Port={port};Database={name};Username={username};Password={password}";
}

string GetPostgresPassword()
{
    return Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Machine)
        ?? throw new InvalidOperationException(
            "POSTGRES_PASSWORD environment variable is not set. Set it with: setx POSTGRES_PASSWORD \"<password>\" and restart your terminal/IDE.");
}
