using System.Reflection;
using HomeSteadier.Database;
using Homesteadier.Repository;
using Homesteadier.Repository.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var sharedConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.shared.json");
builder.Configuration.AddJsonFile(sharedConfigPath, optional: true);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

// Configure DbContext and repositories
var connectionString = BuildConnectionString(builder.Configuration);
builder.Services.AddDbContext<HomesteadierDbContext>(options =>
    options.UseNpgsql(connectionString));

// Auto-register repositories marked with [AutoRegister] attribute
var assembly = typeof(Program).Assembly;
var autoRegisterType = typeof(AutoRegisterAttribute);

foreach (var type in assembly.GetTypes())
{
    if (type.GetCustomAttributes(autoRegisterType, inherit: false).Length > 0)
    {
        // Find the specific repository interface (starts with I, ends with Repository)
        var repositoryInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.Name.StartsWith("I") && i.Name.EndsWith("Repository"));

        if (repositoryInterface != null)
        {
            builder.Services.AddScoped(repositoryInterface, type);
            Console.WriteLine($"Auto-registered: {repositoryInterface.Name} -> {type.Name}");
        }
    }
}

var app = builder.Build();

// Run database migrations before starting the app
await RunDatabaseMigrations(app.Services, app.Configuration);

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "Homesteadier API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

async Task RunDatabaseMigrations(IServiceProvider services, IConfiguration configuration)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var connectionString = BuildConnectionString(configuration);
        var migrationService = new DatabaseMigrationService();

        var result = await migrationService.RunMigrationsAsync(connectionString);

        if (result.Success)
        {
            logger.LogInformation("Database migrations completed successfully");
        }
        else
        {
            logger.LogError("Database migration failed: {Error}", result.Error);
            throw new InvalidOperationException($"Database migration failed: {result.Error}");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error running database migrations");
        throw;
    }
}

string BuildConnectionString(IConfiguration configuration)
{
    var host = configuration["Database:Host"] ?? "localhost";
    var port = configuration["Database:Port"] ?? "5432";
    var name = configuration["Database:Name"]
        ?? throw new InvalidOperationException("Database:Name not found in configuration.");
    var username = configuration["Database:Username"] ?? "postgres";
    var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Machine)
        ?? throw new InvalidOperationException(
            "POSTGRES_PASSWORD environment variable is not set. Set it with: setx POSTGRES_PASSWORD \"<password>\" and restart your terminal/IDE.");

    return $"Host={host};Port={port};Database={name};Username={username};Password={password}";
}
