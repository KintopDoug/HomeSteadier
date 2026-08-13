using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var sharedConfigPath = GetSharedConfigPath();
builder.Configuration.AddJsonFile(sharedConfigPath, optional: false);

// Compute target for `azd`/`aspire` publish: everything below is packaged as containers
// into a single Azure Container Apps environment. No-op in run mode (local dev is unchanged).
builder.AddAzureContainerAppEnvironment("homesteadier-aca-env");

// Read values from configuration
var databaseName = builder.Configuration["Database:Name"];
var databasePort = builder.Configuration["Database:Port"];
var projectName = builder.Configuration["ProjectName"];

// Postgres password comes from a machine-level environment variable so it's the same
// value used by Aspire and the CLI. Set it with:
//   setx POSTGRES_PASSWORD "<password>"
// setx only updates the registry, not the current process's inherited environment block,
// so fall back to the User/Machine registry-backed targets in case the terminal/IDE
// hosting this process hasn't been restarted since the variable was set.
// In publish mode the AppHost runs on a build/deploy machine that has no POSTGRES_PASSWORD;
// the value is supplied at deploy time (azd prompts and stores it as a secure parameter),
// so we register a bare secret parameter and skip the local env-var lookup entirely.
// Publish mode: pass no password parameter. WithPasswordAuthentication then generates a
// strong password automatically and stores it in the Key Vault it provisions for the
// Flexible Server; the API reads it from there via managed identity. Nothing is prompted,
// typed, or committed, and the generated value is persisted in the azd environment so it
// stays stable across redeploys.
// Run mode: source the password from POSTGRES_PASSWORD so it matches the value the CLI
// (migrations) and the existing local data volume were initialized with.
IResourceBuilder<ParameterResource>? postgresPassword = null;
if (!builder.ExecutionContext.IsPublishMode)
{
    var postgresPasswordValue = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Machine)
        ?? throw new InvalidOperationException(
            "POSTGRES_PASSWORD environment variable is not set. Set it with: setx POSTGRES_PASSWORD \"<password>\" and restart your terminal/IDE.");

    postgresPassword = builder.AddParameter("postgres-password", postgresPasswordValue, secret: true);
}

// Publish mode provisions an Azure Database for PostgreSQL Flexible Server (managed,
// persistent, backed up) — a container's data dir can't live on the Azure Files volume
// ACA gives it, so a containerized Postgres crash-loops in the cloud. RunAsContainer keeps
// local dev on the pgvector container. WithPasswordAuthentication uses
// SQL auth with our existing password parameter (stored in Key Vault when deployed), so the
// injected connection string carries a username/password the API can use without Entra wiring.
// Pin the admin username so it's stable and known. Without this, WithPasswordAuthentication
// generates a random name (e.g. "MYAHWGeHeC") that (a) changes if secrets are reset and
// (b) won't exist in a data volume initialized under a different user. "postgres"/"admin"
// are reserved by Azure Flexible Server and can't be the admin login, so use a custom name.
var postgresUser = builder.AddParameter("postgres-user", "homesteadier_admin");

// JWT signing key (HS256 requires >= 32 bytes; the API fails fast at startup otherwise).
// Same split as the Postgres password:
// - Publish: let azd generate a strong 64-char key and persist it as a secret parameter
//   (Key Vault-backed), so it's stable across deploys and never typed, prompted, or committed.
// - Run: source from the JWT_SIGNING_KEY machine env var so it matches local dev.
IResourceBuilder<ParameterResource> jwtSigningKey;
if (builder.ExecutionContext.IsPublishMode)
{
    jwtSigningKey = builder.AddParameter("jwt-signing-key",
        new GenerateParameterDefault { MinLength = 64, Special = false },
        secret: true);
}
else
{
    var jwtSigningKeyValue = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY", EnvironmentVariableTarget.Process)
        ?? Environment.GetEnvironmentVariable("JWT_SIGNING_KEY", EnvironmentVariableTarget.User)
        ?? Environment.GetEnvironmentVariable("JWT_SIGNING_KEY", EnvironmentVariableTarget.Machine)
        ?? throw new InvalidOperationException(
            "JWT_SIGNING_KEY environment variable is not set. Set it with: setx JWT_SIGNING_KEY \"<32+ char secret>\" and restart your terminal/IDE.");

    jwtSigningKey = builder.AddParameter("jwt-signing-key", jwtSigningKeyValue, secret: true);
}

var postgres = builder.AddAzurePostgresFlexibleServer("pgsql")
    .WithPasswordAuthentication(userName: postgresUser, password: postgresPassword)
    .RunAsContainer(container => container
        .WithImage("pgvector/pgvector", "pg17")
        .WithDataVolume(databaseName)
        .WithHostPort(5432));

var db = postgres.AddDatabase(databaseName);

var api = builder.AddProject<Projects.Homesteadier_API>($"{projectName}-API")
            .WithReference(db)
            .WaitFor(db)
            // The API reads JWT_SIGNING_KEY from the environment (Program.cs); inject it so the
            // deployed container gets the generated key and doesn't fail fast at startup.
            .WithEnvironment("JWT_SIGNING_KEY", jwtSigningKey);

// Without this, DCP fronts the API's endpoints with a proxy that binds the ports
// (5128/7131, from launchSettings.json) for the AppHost's whole lifetime — so stopping
// the API in the dashboard doesn't free them, and launching the API's own 'https'
// profile from the IDE fails with "port already in use". Binding directly means the
// ports are released as soon as the resource stops. Called as a statement rather than
// chained because it widens the builder's generic type, which `WithReference(api)` below
// can't consume.
api.WithEndpointProxySupport(false);

// The browser calls the API directly (SPA -> API over the public internet), so the API needs
// external ingress in ACA — internal service-to-service reachability isn't enough.
api.WithExternalHttpEndpoints();

var reactFrontend = builder.AddJavaScriptApp("react-frontend", "../ReactApp")
    .WithReference(api)
    .WaitFor(api)
    // Injected as a container env var; the container's entrypoint writes it into config.js at
    // startup, which the SPA reads (a static bundle can't read runtime env directly). Points
    // at the API's public HTTPS URL.
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("https"))
    // Pinned to 5173 (Vite's own default) to match Cors:AllowedOrigins in
    // appsettings.shared.json — without a fixed port, Aspire assigns a random one
    // each run and the browser rejects the API's response for CORS mismatch.
    // targetPort is pinned to 5173 too so the published nginx container (which listens on
    // 5173, see ReactApp/nginx.conf) matches the port ACA routes ingress + startup probe to.
    // isProxied: false because this non-container resource can't be fronted by a DCP proxy
    // when port == targetPort (the proxy's listen and forward ports would collide); the dev
    // server binds 5173 directly instead.
    .WithHttpEndpoint(port: 5173, targetPort: 5173, env: "VITE_PORT", isProxied: false)
    .WithExternalHttpEndpoints()
    // Run mode uses the Vite dev server; publish mode builds the SPA and serves it from
    // the nginx image described by ReactApp/Dockerfile.
    .PublishAsDockerFile();

// Give the API the frontend's deployed origin for CORS. The URL isn't known until deploy, so
// inject it as an extra Cors:AllowedOrigins entry (index 6 — after the six localhost dev
// origins in appsettings.shared.json). Env key uses "__" so .NET maps it to Cors:AllowedOrigins:6.
// No WaitFor in this direction: the frontend already waits on the API, and adding the reverse
// would be a startup cycle — a plain endpoint reference resolves from the deterministic ACA
// ingress URL without imposing ordering.
api.WithEnvironment("Cors__AllowedOrigins__6", reactFrontend.GetEndpoint("http"));



builder.Build().Run();

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
