using HomeSteadier.CLI.Services;
using Microsoft.Extensions.Configuration;

var sharedConfigPath = GetSharedConfigPath();
var localConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

var configuration = new ConfigurationBuilder()
    .AddJsonFile(sharedConfigPath, optional: false)
    .AddJsonFile(localConfigPath, optional: true)
    .AddEnvironmentVariables()
    .Build();

var databaseService = new DatabaseService();
var modelGenerationService = new DotnetService();
var packageGenerationService = new PackageGenerationService();

Console.WriteLine("HomeSteadier CLI");
Console.WriteLine();

// Handle command-line arguments
if (args.Length > 0)
{
    await ProcessCommand(args, databaseService, modelGenerationService, packageGenerationService, configuration);
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
    await ProcessCommand(parts, databaseService, modelGenerationService, packageGenerationService, configuration);

    Console.WriteLine();
}

async Task ProcessCommand(string[] parts, DatabaseService databaseService, DotnetService dotnetService, PackageGenerationService packageGenerationService, IConfiguration configuration)
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

        case ["database", "gen"]:
            await databaseService.GenerateTableScriptsAsync(configuration);
            break;

        case ["database", "seed"]:
            await databaseService.SeedAsync(configuration);
            break;

        case ["dotnet", "gen"]:
            await dotnetService.GenerateModelsAsync(configuration);
            break;

        case ["packages", "gen"]:
            await packageGenerationService.GenerateAsync(configuration);
            break;

        default:
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Unknown command: '{string.Join(" ", parts)}'. Type 'help' for available commands.");
            Console.ResetColor();
            break;
    }
}

string GetSharedConfigPath()
{
    var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
    return Path.Combine(solutionRoot, "appsettings.shared.json");
}

string FindSolutionRoot(string startPath)
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

void PrintHelp()
{
    Console.WriteLine("Available commands:");
    Console.WriteLine("  database update    Run pending DbUp migrations");
    Console.WriteLine("  database gen       Create/update a CREATE TABLE script per table in");
    Console.WriteLine("                     HomeSteadier.Database/Tables from the live schema");
    Console.WriteLine("  database seed      Import each .csv file in HomeSteadier.Database/Seeds into");
    Console.WriteLine("                     the table it's named after, validating CSV columns against");
    Console.WriteLine("                     the live schema first");
    Console.WriteLine("  dotnet gen         Scaffold entity models + DbContext from the database schema,");
    Console.WriteLine("                     then generate repositories for any new entities");
    Console.WriteLine("                     (existing repositories are skipped, not overwritten)");
    Console.WriteLine("  packages gen       Generate TypeScript request/response models in ReactApp/src/models");
    Console.WriteLine("                     and axios API clients in ReactApp/src/api, both from the API's");
    Console.WriteLine("                     OpenAPI document (requires the API to be running)");
    Console.WriteLine("  help               Show this help message");
    Console.WriteLine("  exit               Exit the CLI");
}
