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
PrintHelp();
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
            await databaseService.RunMigrationsAsync(configuration);
            break;

        case ["dotnet", "gen"]:
            await modelGenerationService.GenerateModelsAsync(configuration);
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
    Console.WriteLine("  dotnet gen         Generate entity models from database schema");
    Console.WriteLine("  help               Show this help message");
    Console.WriteLine("  exit               Exit the CLI");
}
