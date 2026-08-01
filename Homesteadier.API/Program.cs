
using System.Text;
using Homesteadier.API.Auth;
using HomeSteadier.Database;
using HomeSteadier.Models.Database;
using Homesteadier.Repository;
using Homesteadier.Repository.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var sharedConfigPath = GetSharedConfigPath();
builder.Configuration.AddJsonFile(sharedConfigPath, optional: true);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    // Enable the "Authorize" button so JWT-protected endpoints can be tested from Swagger UI.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Paste the JWT returned by /api/auth/login (no \"Bearer \" prefix needed).",
    });

    options.AddSecurityRequirement(_ => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", null, null)] = new List<string>(),
    });
});

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

// Configure ASP.NET Core Identity on top of the existing users table (custom UserStore).
builder.Services.AddIdentityCore<User>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
})
.AddUserStore<UserStore>();

// JWT bearer authentication
var jwtSettings = BuildJwtSettings(builder.Configuration);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep original claim names (e.g. "sub") instead of remapping to legacy XML URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
        };
    });

builder.Services.AddAuthorization();

// Refresh-token service + cookie configuration
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

var cookieSettings = new RefreshCookieSettings();
builder.Configuration.GetSection("RefreshCookie").Bind(cookieSettings);
builder.Services.AddSingleton(cookieSettings);

// CORS — the httpOnly refresh cookie requires credentialed cross-origin requests from the SPA,
// which in turn requires an explicit origin allow-list (wildcard origins can't use credentials).
const string SpaCorsPolicy = "SpaCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(SpaCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

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

app.UseCors(SpaCorsPolicy);

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

JwtSettings BuildJwtSettings(IConfiguration configuration)
{
    var settings = new JwtSettings();
    configuration.GetSection("Jwt").Bind(settings);

    // The signing key is a secret; source it from the environment like POSTGRES_PASSWORD.
    settings.SigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY", EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable("JWT_SIGNING_KEY", EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable("JWT_SIGNING_KEY", EnvironmentVariableTarget.Machine)
        ?? throw new InvalidOperationException(
            "JWT_SIGNING_KEY environment variable is not set. Set it with: setx JWT_SIGNING_KEY \"<32+ char secret>\" and restart your terminal/IDE.");

    // HS256 requires a key of at least 128 bits (16 bytes); fail fast with a clear message at
    // startup rather than throwing on the first token issued. 32+ bytes is recommended.
    var keyBytes = Encoding.UTF8.GetByteCount(settings.SigningKey);
    if (keyBytes < 32)
    {
        throw new InvalidOperationException(
            $"JWT_SIGNING_KEY is too short ({keyBytes} bytes). HS256 signing requires at least 32 bytes; " +
            "set JWT_SIGNING_KEY to a random string of 32+ characters and restart your terminal/IDE.");
    }

    return settings;
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
