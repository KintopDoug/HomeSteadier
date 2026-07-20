
using HomeSteadier.Database;
using HomeSteadier.Models.Security;
using Homesteadier.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var sharedConfigPath = GetSharedConfigPath();
builder.Configuration.AddJsonFile(sharedConfigPath, optional: true);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

// Configure Clerk authentication. Clerk issues standard RS256 JWTs; the API validates
// them against Clerk's JWKS (fetched from the Authority) with no Clerk-specific SDK.
var clerkAuthority = builder.Configuration["Clerk:Authority"]
    ?? throw new InvalidOperationException("Clerk:Authority not found in configuration.");
var authorizedParties = builder.Configuration.GetSection("Clerk:AuthorizedParties").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = clerkAuthority;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = clerkAuthority,
            // Clerk session tokens carry no "aud" claim by default. Defense-in-depth
            // is provided by the optional "azp" (authorized party) check below, which
            // is enforced only when Clerk:AuthorizedParties is non-empty (set the real
            // origin(s) in production; left empty in dev where the frontend port is
            // dynamic under the Aspire host).
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = ClaimTypes.Sub.Value(),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var azp = context.Principal?.FindFirst(ClaimTypes.Azp.Value())?.Value;
                if (authorizedParties.Length > 0 &&
                    (string.IsNullOrEmpty(azp) || !authorizedParties.Contains(azp)))
                {
                    context.Fail("Invalid 'azp' (authorized party) claim.");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Configure DbContext and repositories
var connectionString = BuildConnectionString(builder.Configuration);
builder.Services.AddDbContext<HomesteadierDbContext>(options =>
    options.UseNpgsql(connectionString));

// Auto-register repositories marked with [AutoRegister] attribute
var autoRegisterType = typeof(AutoRegisterAttribute);
var repositoryAssembly = typeof(HomesteadierDbContext).Assembly;

foreach (var type in repositoryAssembly.GetTypes())
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

app.UseAuthentication();
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
