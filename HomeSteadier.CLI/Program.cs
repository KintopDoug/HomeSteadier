using HomeSteadier.Database;
using Microsoft.Extensions.Configuration;
using Npgsql;

var sharedConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.shared.json");
var localConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

var configuration = new ConfigurationBuilder()
    .AddJsonFile(sharedConfigPath, optional: false)
    .AddJsonFile(localConfigPath, optional: true)
    .AddEnvironmentVariables()
    .Build();

var migrationService = new DatabaseMigrationService();

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

        case ["dotnet", "gen"]:
            await GenerateModels(configuration);
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

async Task RunMigrations(IConfiguration configuration)
{
    try
    {
        var connectionString = GetConnectionString(configuration);
        Console.WriteLine("Running database migrations...");

        var result = await migrationService.RunMigrationsAsync(connectionString);

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

async Task GenerateModels(IConfiguration configuration)
{
    try
    {
        var connectionString = GetConnectionString(configuration);
        Console.WriteLine("Generating entity models from database schema...");

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Console.WriteLine("Connected to database.");

        var tables = await GetUserDefinedTables(connection);

        if (tables.Count == 0)
        {
            Console.WriteLine("No user-defined tables found.");
            return;
        }

        var modelsPath = GetModelsPath();
        Console.WriteLine($"Generating models to: {modelsPath}");

        foreach (var table in tables)
        {
            // Skip DbUp's internal schema tracking table
            if (table.TableName.Equals("schemaversions", StringComparison.OrdinalIgnoreCase))
                continue;

            var columns = await GetTableColumns(connection, table.SchemaName, table.TableName);
            var className = ToPascalCase(Singularize(table.TableName));
            var classCode = GenerateEntityClass(className, columns);

            var filePath = Path.Combine(modelsPath, $"{className}.cs");
            await File.WriteAllTextAsync(filePath, classCode);
            Console.WriteLine($"Generated: {className}.cs");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nSuccessfully generated {tables.Count} model(s)!");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error generating models: {ex.Message}");
        Console.ResetColor();
    }
}

async Task<List<(string SchemaName, string TableName)>> GetUserDefinedTables(NpgsqlConnection connection)
{
    var tables = new List<(string, string)>();

    const string query = """
        SELECT table_schema, table_name
        FROM information_schema.tables
        WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
          AND table_type = 'BASE TABLE'
        ORDER BY table_name
        """;

    using var cmd = new NpgsqlCommand(query, connection);
    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        tables.Add((reader.GetString(0), reader.GetString(1)));
    }

    return tables;
}

async Task<List<(string Name, string SqlType, bool IsNullable)>> GetTableColumns(NpgsqlConnection connection, string schemaName, string tableName)
{
    var columns = new List<(string, string, bool)>();

    const string query = """
        SELECT
            column_name,
            udt_name,
            is_nullable,
            ordinal_position
        FROM information_schema.columns
        WHERE table_schema = @schema AND table_name = @table
        ORDER BY ordinal_position
        """;

    using var cmd = new NpgsqlCommand(query, connection);
    cmd.Parameters.AddWithValue("@schema", schemaName);
    cmd.Parameters.AddWithValue("@table", tableName);

    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var columnName = reader.GetString(0);
        var sqlType = reader.GetString(1);
        var isNullable = reader.GetString(2) == "YES";

        columns.Add((columnName, sqlType, isNullable));
    }

    return columns;
}

string GenerateEntityClass(string className, List<(string Name, string SqlType, bool IsNullable)> columns)
{
    var properties = string.Join(Environment.NewLine,
        columns.Select(c => GenerateProperty(c.Name, c.SqlType, c.IsNullable)));

    return "namespace HomeSteadier.Models.Database;\n\n" +
           $"public class {className}\n" +
           "{\n" +
           properties + "\n" +
           "}";
}

string GenerateProperty(string columnName, string sqlType, bool isNullable)
{
    var cSharpType = MapSqlTypeToCSharp(sqlType, isNullable);
    var propertyName = ToPascalCase(columnName);

    return $"    public {cSharpType} {propertyName} {{ get; set; }}";
}

string MapSqlTypeToCSharp(string sqlType, bool isNullable)
{
    var baseType = sqlType.ToLowerInvariant() switch
    {
        "int4" or "serial" => "int",
        "int8" or "bigserial" => "long",
        "bool" => "bool",
        "timestamp" or "timestamptz" => "DateTime",
        "varchar" or "text" or "bpchar" => "string",
        "uuid" => "Guid",
        "numeric" or "decimal" => "decimal",
        "float4" => "float",
        "float8" => "double",
        "date" => "DateOnly",
        "time" or "timetz" => "TimeOnly",
        _ => "string"
    };

    if (isNullable && (baseType == "string" || baseType == "Guid"))
        return $"{baseType}?";

    if (isNullable && baseType != "string" && baseType != "Guid")
        return $"{baseType}?";

    return baseType;
}

string ToPascalCase(string text)
{
    if (string.IsNullOrEmpty(text)) return text;

    var parts = text.Split('_');
    return string.Concat(parts.Select(p =>
        char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..].ToLowerInvariant() : string.Empty)));
}

string Singularize(string plural)
{
    if (plural.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
        return plural[..^3] + "y";
    if (plural.EndsWith("es", StringComparison.OrdinalIgnoreCase))
        return plural[..^2];
    if (plural.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        return plural[..^1];

    return plural;
}

string GetModelsPath()
{
    var currentDir = AppContext.BaseDirectory;
    var solutionDir = FindSolutionRoot(currentDir);
    return Path.Combine(solutionDir, "HomeSteadier.Models", "Database");
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

string GetPostgresPassword()
{
    return Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Machine)
        ?? throw new InvalidOperationException(
            "POSTGRES_PASSWORD environment variable is not set. Set it with: setx POSTGRES_PASSWORD \"<password>\" and restart your terminal/IDE.");
}
