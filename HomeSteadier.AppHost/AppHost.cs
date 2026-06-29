var builder = DistributedApplication.CreateBuilder(args);

// Read database name from configuration
var databaseName = builder.Configuration["Database:Name"];

// Add PostgreSQL with init script for conditional database creation
var postgres = builder.AddPostgres("pgsql")
    .WithDataVolume(databaseName)
    .WithBindMount("./postgres-init.sql", "/docker-entrypoint-initdb.d/01-init.sql", isReadOnly: true)
    .WithLifetime(ContainerLifetime.Persistent) // Keeps container alive 
    .WithHostPort(5432);

var db = postgres.AddDatabase(databaseName);

builder.Build().Run();
