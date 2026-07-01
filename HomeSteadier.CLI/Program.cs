using HomeSteadier.CLI.Services;
using Microsoft.Extensions.Configuration;

var sharedConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.shared.json");
var localConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

var configuration = new ConfigurationBuilder()
    .AddJsonFile(sharedConfigPath, optional: false)
    .AddJsonFile(localConfigPath, optional: true)
    .AddEnvironmentVariables()
    .Build();

var databaseService = new DatabaseService();
var modelGenerationService = new DotnetService();

Console.WriteLine("HomeSteadier CLI");
Console.WriteLine();

// Handle command-line arguments
if (args.Length > 0)
{
    await ProcessCommand(args, databaseService, modelGenerationService, configuration);
    return 0;
}

PrintHelp();
Console.WriteLine();

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input))
        continue;

    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    await ProcessCommand(parts, databaseService, modelGenerationService, configuration);

    Console.WriteLine();
}

async Task ProcessCommand(string[] parts, DatabaseService databaseService, DotnetService modelGenerationService, IConfiguration configuration)
{
    switch (parts)
    {
        case ["exit"] or ["quit"]:
            Console.WriteLine("Goodbye!");
            Environment.Exit(0);
            break;

        case ["help"]:
            PrintHelp();
            break;

        case ["database", "update"]:
            await databaseService.RunMigrationsAsync(configuration);
            break;

        case ["dotnet", "gen"] or ["gen"]:
            await modelGenerationService.GenerateModelsAsync(configuration);
            break;

        default:
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Unknown command: '{string.Join(" ", parts)}'. Type 'help' for available commands.");
            Console.ResetColor();
            break;
    }
}

void PrintHelp()
{
    Console.WriteLine("Available commands:");
    Console.WriteLine("  database update    Run pending migrations");
    Console.WriteLine("  dotnet gen         Generate entity models from database schema");
    Console.WriteLine("  help               Show this help message");
    Console.WriteLine("  exit               Exit the CLI");
}
