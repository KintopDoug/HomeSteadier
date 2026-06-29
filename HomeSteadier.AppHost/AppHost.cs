var builder = DistributedApplication.CreateBuilder(args);

// Read values from configuration
var databaseName = builder.Configuration["Database:Name"];
var projectName = builder.Configuration["ProjectName"];

// Add PostgreSQL with init script for conditional database creation
var postgres = builder.AddPostgres("pgsql")
    .WithDataVolume(databaseName)
    .WithHostPort(5432);

var db = postgres.AddDatabase(databaseName);

builder.AddProject<Projects.Homesteadier_API>(projectName);

builder.Build().Run();
