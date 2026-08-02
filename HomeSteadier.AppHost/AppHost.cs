using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var sharedConfigPath = GetSharedConfigPath();
builder.Configuration.AddJsonFile(sharedConfigPath, optional: false);

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
var postgresPasswordValue = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Process)
    ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.User)
    ?? Environment.GetEnvironmentVariable("POSTGRES_PASSWORD", EnvironmentVariableTarget.Machine)
    ?? throw new InvalidOperationException(
        "POSTGRES_PASSWORD environment variable is not set. Set it with: setx POSTGRES_PASSWORD \"<password>\" and restart your terminal/IDE.");

var postgresPassword = builder.AddParameter("postgres-password", postgresPasswordValue, secret: true);

// Add PostgreSQL with init script for conditional database creation. use pgvector image to enable vector search capabilities.
var postgres = builder.AddPostgres("pgsql", password: postgresPassword)
    .WithImage("pgvector/pgvector", "pg17")
    .WithDataVolume(databaseName)
    .WithHostPort(5432);

var db = postgres.AddDatabase(databaseName);

var api = builder.AddProject<Projects.Homesteadier_API>($"{projectName}-API")
            .WithReference(db)
            .WaitFor(db);

// Without this, DCP fronts the API's endpoints with a proxy that binds the ports
// (5128/7131, from launchSettings.json) for the AppHost's whole lifetime — so stopping
// the API in the dashboard doesn't free them, and launching the API's own 'https'
// profile from the IDE fails with "port already in use". Binding directly means the
// ports are released as soon as the resource stops. Called as a statement rather than
// chained because it widens the builder's generic type, which `WithReference(api)` below
// can't consume.
api.WithEndpointProxySupport(false);

builder.AddJavaScriptApp("react-frontend", "../ReactApp")
    .WithReference(api)
    .WaitFor(api)
    // Feeds the API endpoint into typeScript process env
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("https"))
    // Pinned to 5173 (Vite's own default) to match Cors:AllowedOrigins in
    // appsettings.shared.json — without a fixed port, Aspire assigns a random one
    // each run and the browser rejects the API's response for CORS mismatch.
    .WithHttpEndpoint(port: 5173, env: "VITE_PORT")
    .WithExternalHttpEndpoints();



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
