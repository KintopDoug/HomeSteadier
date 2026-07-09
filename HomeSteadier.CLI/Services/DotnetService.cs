using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace HomeSteadier.CLI.Services;

public class DotnetService
{
    public async Task GenerateModelsAsync(IConfiguration configuration)
    {
        try
        {
            var connectionString = GetConnectionString(configuration);
            Console.WriteLine("Generating entity models from database schema using EF Core scaffolding...");

            var modelsPath = GetModelsPath();
            var repositoriesPath = GetRepositoriesPath();

            Console.WriteLine($"Generating models to: {modelsPath}");
            Console.WriteLine($"Generating repositories to: {repositoriesPath}");

            // Ensure directories exist
            Directory.CreateDirectory(modelsPath);
            Directory.CreateDirectory(repositoriesPath);

            var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory);
            var repositoryProjectPath = Path.Combine(solutionRoot, "Homesteadier.Repository");

            // Scaffold entities into an isolated temp folder so pre-existing project files
            // (Repository.cs, IRepository.cs, AutoRegisterAttribute.cs, etc.) are never mistaken
            // for generated output. Only the DbContext is written directly into the project.
            const string tempFolderName = "_ScaffoldTemp";
            var tempOutputPath = Path.Combine(repositoryProjectPath, tempFolderName);

            if (Directory.Exists(tempOutputPath))
                Directory.Delete(tempOutputPath, recursive: true);
            Directory.CreateDirectory(tempOutputPath);

            // Run EF Core scaffolding via dotnet ef command
            // Note: Must run from Repository project directory where EF Core Design package is installed
            var scaffoldProcess = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"ef dbcontext scaffold \"{connectionString}\" Npgsql.EntityFrameworkCore.PostgreSQL " +
                           $"--output-dir {tempFolderName} " +
                           $"--context HomesteadierDbContext " +
                           $"--context-dir . " +
                           $"--namespace HomeSteadier.Models.Database " +
                           $"--context-namespace Homesteadier.Repository " +
                           $"--force " +
                           $"--no-onconfiguring",
                WorkingDirectory = repositoryProjectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Console.WriteLine($"Running scaffolding from: {repositoryProjectPath}");

            using var process = Process.Start(scaffoldProcess);
            if (process == null)
                throw new InvalidOperationException("Failed to start scaffolding process");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            Console.WriteLine(output);

            if (process.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nScaffolding failed with exit code {process.ExitCode}");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine("\n=== Error Output ===");
                    Console.WriteLine(error);
                    Console.WriteLine("=== End Error Output ===\n");
                }
                Console.ResetColor();
                if (Directory.Exists(tempOutputPath))
                    Directory.Delete(tempOutputPath, recursive: true);
                return;
            }

            // Move generated models from the isolated temp folder to the Models project
            var skippedEntities = MoveGeneratedModels(tempOutputPath, modelsPath);

            if (Directory.Exists(tempOutputPath))
                Directory.Delete(tempOutputPath, recursive: true);

            // Strip references to skipped entities (e.g. DbUp's migrations table) from the
            // freshly scaffolded DbContext, since no model file exists for them.
            if (skippedEntities.Count > 0)
            {
                var dbContextPath = Path.Combine(repositoryProjectPath, "HomesteadierDbContext.cs");
                await RemoveEntitiesFromDbContextAsync(dbContextPath, skippedEntities);
            }

            // Get list of generated entity files
            var generatedEntities = GetGeneratedEntities(modelsPath);

            Console.WriteLine($"Generated {generatedEntities.Count} entity model(s)");

            // Generate repositories for each entity
            var generatedCount = 0;
            var skippedRepositories = new List<string>();

            foreach (var entityName in generatedEntities)
            {
                var interfaceFilePath = Path.Combine(repositoriesPath, $"I{entityName}Repository.cs");
                var implementationFilePath = Path.Combine(repositoriesPath, $"{entityName}Repository.cs");

                if (File.Exists(interfaceFilePath) || File.Exists(implementationFilePath))
                {
                    skippedRepositories.Add(entityName);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Skipped: I{entityName}Repository.cs (already exists)");
                    Console.ResetColor();
                }
                else
                {
                    var interfaceCode = GenerateRepositoryInterface(entityName);
                    var implementationCode = GenerateRepositoryImplementation(entityName);

                    await File.WriteAllTextAsync(interfaceFilePath, interfaceCode);
                    Console.WriteLine($"Generated: {interfaceFilePath}");

                    await File.WriteAllTextAsync(implementationFilePath, implementationCode);
                    Console.WriteLine($"Generated: {implementationFilePath}");

                    generatedCount++;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nSuccessfully generated {generatedEntities.Count} model(s), {generatedCount} repository/repositories, and DbContext!");
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
            if (ex.InnerException != null)
                Console.WriteLine($"Inner error: {ex.InnerException.Message}");
            Console.ResetColor();
        }
    }

    private List<string> MoveGeneratedModels(string sourceDir, string targetDir)
    {
        var skippedEntities = new List<string>();

        if (!Directory.Exists(sourceDir))
            return skippedEntities;

        // Find all .cs files that are entity models (not DbContext files)
        var files = Directory.GetFiles(sourceDir, "*.cs");
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var className = Path.GetFileNameWithoutExtension(file);

            // Skip DbContext files - they stay in Repository
            if (fileName.Contains("DbContext"))
                continue;

            // Skip migrations table model - it's managed by DbUp, not EF Core
            if (fileName.Contains("Migration", StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Skipped: {fileName} (DbUp-managed table)");
                Console.ResetColor();
                skippedEntities.Add(className);
                try
                {
                    File.Delete(file);
                }
                catch { /* Ignore deletion errors */ }
                continue;
            }

            var targetPath = Path.Combine(targetDir, fileName);
            try
            {
                // Move the file to Models directory
                if (File.Exists(targetPath))
                    File.Delete(targetPath);

                File.Move(file, targetPath);
                Console.WriteLine($"Moved {fileName} to models directory");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Could not move {fileName}: {ex.Message}");
                Console.ResetColor();
            }
        }

        return skippedEntities;
    }

    private async Task RemoveEntitiesFromDbContextAsync(string dbContextPath, List<string> entityClassNames)
    {
        if (!File.Exists(dbContextPath))
            return;

        var content = await File.ReadAllTextAsync(dbContextPath);
        var modified = false;

        foreach (var entityClassName in entityClassNames)
        {
            // Remove the DbSet<{Entity}> property declaration line
            var dbSetPattern = $@"[ \t]*public virtual DbSet<{Regex.Escape(entityClassName)}>.*\r?\n\r?\n?";
            var newContent = Regex.Replace(content, dbSetPattern, string.Empty);
            if (newContent != content)
            {
                content = newContent;
                modified = true;
            }

            // Remove the modelBuilder.Entity<{Entity}>(entity => { ... }); configuration block
            var configPattern = $@"[ \t]*modelBuilder\.Entity<{Regex.Escape(entityClassName)}>\(entity =>\s*\{{.*?\}}\);\r?\n\r?\n?";
            newContent = Regex.Replace(content, configPattern, string.Empty, RegexOptions.Singleline);
            if (newContent != content)
            {
                content = newContent;
                modified = true;
            }
        }

        if (modified)
        {
            await File.WriteAllTextAsync(dbContextPath, content);
            Console.WriteLine($"Removed DbUp-managed entity reference(s) from DbContext: {string.Join(", ", entityClassNames)}");
        }
    }

    private List<string> GetGeneratedEntities(string modelsPath)
    {
        var entities = new List<string>();

        if (!Directory.Exists(modelsPath))
            return entities;

        var files = Directory.GetFiles(modelsPath, "*.cs");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            // Skip DbContext files
            if (!fileName.Contains("DbContext"))
            {
                entities.Add(fileName);
            }
        }

        return entities;
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
