using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HomeSteadier.CLI.Services;

public class DotnetService
{
    public async Task GenerateModelsAsync(IConfiguration configuration)
    {
        try
        {
            var connectionString = GetConnectionString(configuration);
            Console.WriteLine("Generating entity models from database schema...");

            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine("Connected to database.");

            var tables = await GetUserDefinedTablesAsync(connection);

            if (tables.Count == 0)
            {
                Console.WriteLine("No user-defined tables found.");
                return;
            }

            var modelsPath = GetModelsPath();
            Console.WriteLine($"Generating models to: {modelsPath}");

            var repositoriesPath = GetRepositoriesPath();
            Console.WriteLine($"Generating repositories to: {repositoriesPath}");

            var generatedCount = 0;
            var skippedRepositories = new List<string>();

            foreach (var table in tables)
            {
                // Skip DbUp's internal schema tracking table
                if (table.TableName.Equals("migrations", StringComparison.OrdinalIgnoreCase))
                    continue;

                var columns = await GetTableColumnsAsync(connection, table.SchemaName, table.TableName);
                var className = ToPascalCase(Singularize(table.TableName));
                var classCode = GenerateEntityClass(className, columns);

                var filePath = Path.Combine(modelsPath, $"{className}.cs");
                await File.WriteAllTextAsync(filePath, classCode);
                Console.WriteLine($"Generated: {className}.cs");
                generatedCount++;

                // Generate repository interface and implementation
                var interfaceFilePath = Path.Combine(repositoriesPath, $"I{className}Repository.cs");
                var implementationFilePath = Path.Combine(repositoriesPath, $"{className}Repository.cs");

                if (File.Exists(interfaceFilePath) || File.Exists(implementationFilePath))
                {
                    skippedRepositories.Add(className);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Skipped: I{className}Repository.cs (already exists)");
                    Console.ResetColor();
                }
                else
                {
                    var interfaceCode = GenerateRepositoryInterface(className);
                    var implementationCode = GenerateRepositoryImplementation(className);

                    await File.WriteAllTextAsync(interfaceFilePath, interfaceCode);
                    Console.WriteLine($"Generated: I{className}Repository.cs");

                    await File.WriteAllTextAsync(implementationFilePath, implementationCode);
                    Console.WriteLine($"Generated: {className}Repository.cs");
                }

                // Update DbContext with new entity
                await UpdateDbContextAsync(className, columns);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nSuccessfully generated {generatedCount} model(s) and repositories!");
            if (skippedRepositories.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Skipped {skippedRepositories.Count} existing repository/repositories: {string.Join(", ", skippedRepositories)}");
            }
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error generating models: {ex.Message}");
            Console.ResetColor();
        }
    }

    private async Task<List<(string SchemaName, string TableName)>> GetUserDefinedTablesAsync(NpgsqlConnection connection)
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

    private async Task<List<(string Name, string SqlType, bool IsNullable)>> GetTableColumnsAsync(NpgsqlConnection connection, string schemaName, string tableName)
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

    private string GenerateEntityClass(string className, List<(string Name, string SqlType, bool IsNullable)> columns)
    {
        var properties = string.Join(Environment.NewLine,
            columns.Select(c => GenerateProperty(c.Name, c.SqlType, c.IsNullable)));

        return "namespace HomeSteadier.Models.Database;\n\n" +
               $"public class {className}\n" +
               "{\n" +
               properties + "\n" +
               "}";
    }

    private string GenerateProperty(string columnName, string sqlType, bool isNullable)
    {
        var cSharpType = MapSqlTypeToCSharp(sqlType, isNullable);
        var propertyName = ToPascalCase(columnName);

        return $"    public {cSharpType} {propertyName} {{ get; set; }}";
    }

    private string MapSqlTypeToCSharp(string sqlType, bool isNullable)
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

    private string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // If text contains underscores, split and capitalize each part
        if (text.Contains('_'))
        {
            var parts = text.Split('_');
            return string.Concat(parts.Select(p =>
                char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..].ToLowerInvariant() : string.Empty)));
        }

        // If already in mixed case (e.g., FirstName, IsActive), just capitalize first letter
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private string Singularize(string plural)
    {
        if (plural.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            return plural[..^3] + "y";
        if (plural.EndsWith("es", StringComparison.OrdinalIgnoreCase))
            return plural[..^2];
        if (plural.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return plural[..^1];

        return plural;
    }

    private string GenerateRepositoryInterface(string className)
    {
        return "using HomeSteadier.Models.Database;\n" +
               "using Homesteadier.Repository;\n\n" +
               "namespace Homesteadier.Repository.Repositories;\n\n" +
               $"public interface I{className}Repository : IRepository<{className}>\n" +
               "{\n" +
               "    // Example custom query - uncomment and modify for your needs:\n" +
               "    // Task<" + className + "?> GetByIdAsync(int id);\n" +
               "    //\n" +
               "    // Example filtered collection - uncomment and modify:\n" +
               "    // Task<List<" + className + ">> GetActiveAsync();\n" +
               "}";
    }

    private string GenerateRepositoryImplementation(string className)
    {
        return "using HomeSteadier.Models.Database;\nusing Homesteadier.Repository;\n\n" +
               "namespace Homesteadier.Repository.Repositories;\n\n" +
               "[AutoRegister]\n" +
               $"public class {className}Repository : Repository<{className}>, I{className}Repository\n" +
               "{\n" +
               $"    public {className}Repository(HomesteadierDbContext context)\n" +
               "        : base(context)\n" +
               "    {\n" +
               "    }\n\n" +
               "    // Implement custom query methods here. Example:\n" +
               "    // public async Task<" + className + "?> GetByIdAsync(int id)\n" +
               "    // {\n" +
               "    //     return await _context.Set<" + className + ">()\n" +
               "    //         .FirstOrDefaultAsync(e => e.Id == id);\n" +
               "    // }\n" +
               "}";
    }

    private string GetModelsPath()
    {
        var currentDir = AppContext.BaseDirectory;
        var solutionDir = FindSolutionRoot(currentDir);
        return Path.Combine(solutionDir, "HomeSteadier.Models", "Database");
    }

    private string GetRepositoriesPath()
    {
        var currentDir = AppContext.BaseDirectory;
        var solutionDir = FindSolutionRoot(currentDir);
        return Path.Combine(solutionDir, "Homesteadier.Repository", "Repositories");
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

    private async Task UpdateDbContextAsync(string className, List<(string Name, string SqlType, bool IsNullable)> columns)
    {
        try
        {
            var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
            var dbContextPath = Path.Combine(solutionRoot, "Homesteadier.Repository", "HomesteadierDbContext.cs");

            if (!File.Exists(dbContextPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"DbContext not found at: {dbContextPath}");
                Console.ResetColor();
                return;
            }

            var content = await File.ReadAllTextAsync(dbContextPath);
            var pluralName = Pluralize(className);

            // Check if entity already exists in DbContext (either DbSet or configuration)
            if (content.Contains($"DbSet<{className}>") || content.Contains($"modelBuilder.Entity<{className}>"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Skipped: {className} already in DbContext");
                Console.ResetColor();
                return;
            }

            var modified = false;

            // Add DbSet property before OnModelCreating method
            var dbSetLine = $"    public DbSet<{className}> {pluralName} {{ get; set; }}";
            var onModelCreatingIndex = content.IndexOf("protected override void OnModelCreating");
            if (onModelCreatingIndex >= 0)
            {
                var lineStart = content.LastIndexOf("\n", onModelCreatingIndex) + 1;
                content = content.Insert(lineStart, dbSetLine + Environment.NewLine + Environment.NewLine);
                modified = true;
            }

            // Add entity configuration before the closing brace of OnModelCreating only if it doesn't exist
            if (!content.Contains($"modelBuilder.Entity<{className}>"))
            {
                var configTemplate = GenerateDbContextConfiguration(className, pluralName, columns);
                var methodStart = content.IndexOf("protected override void OnModelCreating");
                if (methodStart >= 0)
                {
                    // Find the closing brace of OnModelCreating method (indented with 4 spaces)
                    var closingBracePattern = Environment.NewLine + "    }";
                    var closingBraceIndex = content.IndexOf(closingBracePattern, methodStart);
                    if (closingBraceIndex >= 0)
                    {
                        content = content.Insert(closingBraceIndex, Environment.NewLine + Environment.NewLine + configTemplate);
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                await File.WriteAllTextAsync(dbContextPath, content);
                Console.WriteLine($"Updated DbContext with {className}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error updating DbContext: {ex.Message}");
            Console.ResetColor();
        }
    }

    private string Pluralize(string singular)
    {
        if (singular.EndsWith("y"))
            return singular[..^1] + "ies";
        if (singular.EndsWith("s") || singular.EndsWith("x") || singular.EndsWith("z"))
            return singular + "es";
        return singular + "s";
    }

    private string GenerateDbContextConfiguration(string className, string pluralName, List<(string Name, string SqlType, bool IsNullable)> columns)
    {
        var tableName = ToCamelCase(pluralName);
        var config = $"        modelBuilder.Entity<{className}>(entity =>\n" +
                     $"        {{\n" +
                     $"            entity.ToTable(\"{tableName}\");\n" +
                     $"            entity.HasKey(e => e.Id);\n\n";

        foreach (var column in columns)
        {
            var propertyName = ToPascalCase(column.Name);
            var columnName = column.Name;
            config += $"            entity.Property(e => e.{propertyName})\n" +
                     $"                .HasColumnName(\"{columnName}\")";

            if (column.Name != "id" && !column.IsNullable)
                config += "\n                .IsRequired()";

            config += ";\n\n";
        }

        config += "        });";
        return config;
    }

    private string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToLowerInvariant(text[0]) + text[1..];
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
