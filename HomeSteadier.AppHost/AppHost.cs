var builder = DistributedApplication.CreateBuilder(args);

// Read database name from configuration
var databaseName = builder.Configuration["Database:Name"];

// Add PostgreSQL with init script for conditional database creation
var postgres = builder.AddPostgres("pgsql")
    .WithDataVolume(databaseName)
    .WithHostPort(5432);

var db = postgres.AddDatabase(databaseName);

builder.Build().Run();
