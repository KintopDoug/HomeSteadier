using Aspire.Hosting;
using k8s.Models;

var builder = DistributedApplication.CreateBuilder(args);

// Read values from configuration
var databaseName = builder.Configuration["Database:Name"];
var projectName = builder.Configuration["ProjectName"];

// Add PostgreSQL with init script for conditional database creation. use pgvector image to enable vector search capabilities.
var postgres = builder.AddPostgres("pgsql")
    .WithImage("pgvector/pgvector", "pg17") // or pg16
    .WithDataVolume(databaseName)
    .WithHostPort(5432);

var db = postgres.AddDatabase(databaseName);

var api = builder.AddProject<Projects.Homesteadier_API>(projectName);

builder.AddJavaScriptApp("react-frontend", "../ReactApp")
    .WithReference(api)
    .WaitFor(api)
    // Feeds the API endpoint into your TypeScript process env
    .WithEnvironment("VITE_API_URL", api.GetEndpoint("https"))
    .WithHttpEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints();



builder.Build().Run();
