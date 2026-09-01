
using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Azure.Communication.Email;
using Homesteadier.API.Auth;
using Homesteadier.API.Email;
using Homesteadier.API.Farms;
using Homesteadier.API.Middleware;
using HomeSteadier.Database;
using HomeSteadier.Models.Database;
using Homesteadier.Repository;
using Homesteadier.Repository.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var sharedConfigPath = GetSharedConfigPath();
builder.Configuration.AddJsonFile(sharedConfigPath, optional: true);

// Re-apply the environment variables provider so it outranks appsettings.shared.json. Config
// sources are last-wins, and CreateBuilder registers environment variables *before* the line
// above — so without this, the shared file silently beats anything Aspire/ACA injects. That
// isn't hypothetical: App__FrontendBaseUrl and Email__SenderAddress are both injected by
// AppHost and both have (deliberately local/empty) defaults in the shared file, so deployed
// reset links would point at localhost and the sender address would never arrive.
// Cors__AllowedOrigins__6/7 escaped this only because they're array indices the file doesn't
// define, which is luck rather than design.
builder.Configuration.AddEnvironmentVariables();

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
{
    // EF navigation properties (e.g. User.RefreshTokens <-> RefreshToken.User) form
    // reference cycles that System.Text.Json can't serialize by default.
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddOpenApi(options =>
{
    // Set OperationId to the controller action name (e.g. "Register", "GetAll") so
    // codegen tools (see HomeSteadier.CLI's 'packages gen') can name generated
    // client methods after the actual C# action rather than guessing from the route.
    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerActionDescriptor)
        {
            operation.OperationId = controllerActionDescriptor.ActionName;
        }

        return Task.CompletedTask;
    });
});
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
            // Defaults to 5 minutes, which would silently stretch a 15-minute access token to
            // ~20. Keep a small allowance for clock drift between issuer and validator instead.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// Brute-force protection for the credential-accepting endpoints (see AuthRateLimiting).
// Partitioned by client IP: the request body isn't buffered at this point, so the submitted
// email isn't available to include in the partition key.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(AuthRateLimiting.PolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = AuthRateLimiting.PermitLimit,
                Window = AuthRateLimiting.Window,
                QueueLimit = 0,
            }));

    // Only set headers here — writing a body would start the response before the middleware
    // applies RejectionStatusCode, which then throws. The SPA keys off the 429 status.
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };
});

// Refresh-token service + cookie configuration
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

var cookieSettings = new RefreshCookieSettings();
builder.Configuration.GetSection("RefreshCookie").Bind(cookieSettings);
builder.Services.AddSingleton(cookieSettings);

// Password reset: token lifetime, the password-replacement service, and the SPA origin that
// emailed reset links point at.
var passwordResetSettings = new PasswordResetSettings();
builder.Configuration.GetSection("PasswordReset").Bind(passwordResetSettings);
builder.Services.AddSingleton(passwordResetSettings);

builder.Services.AddScoped<IPasswordResetTokenService, PasswordResetTokenService>();
builder.Services.AddScoped<IPasswordUpdateService, PasswordUpdateService>();
builder.Services.AddSingleton(new FrontendUrls(ResolveFrontendBaseUrl(builder.Configuration)));

// Farm invitations: token lifetime + the issuing/validating/consuming service, mirroring the
// password-reset setup above.
var farmInvitationSettings = new FarmInvitationSettings();
builder.Configuration.GetSection("FarmInvitation").Bind(farmInvitationSettings);
builder.Services.AddSingleton(farmInvitationSettings);
builder.Services.AddScoped<IFarmInvitationTokenService, FarmInvitationTokenService>();

// Outbound email. The ACS connection string carries an access key, so it comes from the
// environment like JWT_SIGNING_KEY rather than appsettings.shared.json; AppHost injects it into
// the deployed container.
var emailSettings = new EmailSettings();
builder.Configuration.GetSection("Email").Bind(emailSettings);
emailSettings.ConnectionString = ReadEnvironmentVariable("ACS_CONNECTION_STRING");
builder.Services.AddSingleton(emailSettings);

if (!string.IsNullOrWhiteSpace(emailSettings.ConnectionString)
    && !string.IsNullOrWhiteSpace(emailSettings.SenderAddress))
{
    builder.Services.AddSingleton(new EmailClient(emailSettings.ConnectionString));
    builder.Services.AddScoped<IEmailSender, AcsEmailSender>();
}
else if (builder.Environment.IsDevelopment())
{
    // ACS isn't provisioned locally and has no emulator. Log the reset link instead so the whole
    // flow works from a fresh clone with no Azure account.
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
}
else
{
    // Same posture as a missing JWT_SIGNING_KEY: a deployed API that silently drops password
    // reset emails is worse than one that refuses to start.
    throw new InvalidOperationException(
        "ACS_CONNECTION_STRING and Email:SenderAddress must both be configured outside Development. "
        + "Set ACS_CONNECTION_STRING from the Azure Communication Services resource and fill in "
        + "Email:SenderAddress with an address on a verified domain.");
}

// CORS — the httpOnly refresh cookie requires credentialed cross-origin requests from the SPA,
// which in turn requires an explicit origin allow-list (wildcard origins can't use credentials).
const string SpaCorsPolicy = "SpaCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
var allowAspireDevHosts = builder.Environment.IsDevelopment();
builder.Services.AddCors(options =>
{
    options.AddPolicy(SpaCorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    return true;

                // Aspire serves each resource from a generated
                // "{resource}-{app}.dev.localhost" host whose port can change between
                // runs, so match the suffix in Development rather than pinning every
                // hostname/port pair in config. Never allowed outside Development.
                if (!allowAspireDevHosts)
                    return false;

                return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && uri.Host.EndsWith(".dev.localhost", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Apply pending migrations and seed reference data before the app serves any request. Serialized
// across replicas by a Postgres advisory lock inside the initializer; migrations fail-fast (a bad
// migration aborts startup), seeding warns and continues. Seed CSVs ship in the container under
// Seeds/ (copied from HomeSteadier.Database). BuildConnectionString already prefers Aspire's
// injected connection string (the managed Flexible Server in Azure).
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var seedsPath = Path.Combine(AppContext.BaseDirectory, "Seeds");
await new DatabaseInitializer().InitializeAsync(BuildConnectionString(app.Configuration), seedsPath, startupLogger);

app.MapDefaultEndpoints();

// Behind the ACA ingress proxy the real client IP arrives in X-Forwarded-For and the original
// scheme in X-Forwarded-Proto. Apply them onto HttpContext as the very first middleware, before
// anything reads them — the brute-force rate limiter partitions by RemoteIpAddress (see the
// AddRateLimiter policy above), which would otherwise be the single ingress IP, collapsing every
// client into one shared bucket and defeating the limit. KnownNetworks/KnownProxies are cleared
// because the ingress IP isn't known ahead of time; this is safe because the container only ever
// receives traffic through that ingress, and ForwardLimit=1 reads only the entry the ingress
// appended (the true client IP) — any client-supplied X-Forwarded-For values sit to its left and
// are ignored, so they can't spoof their way into fresh rate-limit buckets.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

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

// CORS must run before HTTPS redirection: a request arriving on the HTTP port would
// otherwise get a 307 with no CORS headers, and browsers refuse to follow redirects on a
// preflight — surfacing as an opaque CORS error in the SPA rather than a redirect.
app.UseCors(SpaCorsPolicy);

app.UseHttpsRedirection();

// After UseCors so a 429 still carries CORS headers (otherwise the SPA sees an opaque CORS
// failure instead of the rate-limit response), and so preflights are answered by the CORS
// middleware rather than consuming permits.
app.UseRateLimiter();

app.UseAuthentication();

// After authentication so an authenticated request's user id is available to log, before
// authorization/controller execution so both are covered by its try/catch.
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

// setx only updates the registry, not the current process's inherited environment block, so a
// variable set without restarting the terminal/IDE is only visible via the User/Machine targets.
string? ReadEnvironmentVariable(string name)
    => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);

/// <summary>
/// The SPA origin that emailed links point at. AppHost injects App:FrontendBaseUrl from the
/// frontend's endpoint, and — in publish mode only — App:FrontendBaseUrlOverride composed from
/// the custom-domain parameter. The override is preferred but must be checked rather than
/// trusted: on the first pass of the two-pass managed-cert bootstrap, or in an environment that
/// never sets custom-domain, that expression composes to a bare "https://". Silently emailing
/// every user a link to that is much worse than falling back to the default ACA hostname.
/// </summary>
string ResolveFrontendBaseUrl(IConfiguration configuration)
{
    string?[] candidates =
    [
        configuration["App:FrontendBaseUrlOverride"],
        configuration["App:FrontendBaseUrl"],
    ];

    foreach (var candidate in candidates)
    {
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host))
        {
            return candidate!.TrimEnd('/');
        }
    }

    throw new InvalidOperationException(
        "App:FrontendBaseUrl is not configured with an absolute http(s) URL, so password reset "
        + "links cannot be built. Set it in appsettings.shared.json.");
}

JwtSettings BuildJwtSettings(IConfiguration configuration)
{
    var settings = new JwtSettings();
    configuration.GetSection("Jwt").Bind(settings);

    // The signing key is a secret; source it from the environment like POSTGRES_PASSWORD.
    settings.SigningKey = ReadEnvironmentVariable("JWT_SIGNING_KEY")
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
    var name = configuration["Database:Name"]
        ?? throw new InvalidOperationException("Database:Name not found in configuration.");

    // Under Aspire orchestration (local run via AppHost, or deployed to Azure), the DB
    // connection string is injected by WithReference(db) as ConnectionStrings:<name>. Prefer
    // it: in Azure it points at the managed Flexible Server (localhost is meaningless there,
    // since Postgres is a separate service), and it carries the right host/credentials.
    var injected = configuration.GetConnectionString(name);
    if (!string.IsNullOrWhiteSpace(injected))
        return injected;

    // Fallback for running the API standalone (no Aspire): build from shared config + the
    // POSTGRES_PASSWORD environment variable.
    var host = configuration["Database:Host"] ?? "localhost";
    var port = configuration["Database:Port"] ?? "5432";
    var username = configuration["Database:Username"] ?? "postgres";
    var password = ReadEnvironmentVariable("POSTGRES_PASSWORD")
        ?? throw new InvalidOperationException(
            "POSTGRES_PASSWORD environment variable is not set. Set it with: setx POSTGRES_PASSWORD \"<password>\" and restart your terminal/IDE.");

    return $"Host={host};Port={port};Database={name};Username={username};Password={password}";
}
