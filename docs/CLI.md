# HomeSteadier CLI Guide

The HomeSteadier CLI provides a command-line interface for managing database migrations and other administrative tasks.

## Prerequisites

- **.NET 10.0 SDK** or later
- **POSTGRES_PASSWORD** environment variable set (see [README.md](../README.md#environment-configuration))
- PostgreSQL database accessible (either via Aspire or standalone)

## Building the CLI

```bash
dotnet build HomeSteadier.CLI
```

## Running the CLI

```bash
dotnet run --project HomeSteadier.CLI
```

This launches an interactive REPL:

```
HomeSteadier CLI
Type 'help' for available commands, 'exit' to quit.

>
```

## Available Commands

### `database update`

Runs all pending database migrations against the configured PostgreSQL database.

```
> database update
Running database migrations...
[DbUp output...]

Migrations completed successfully!
```

Migrations are managed using [DbUp](https://github.com/DbUp/DbUp) and defined as SQL scripts in `HomeSteadier.Database/Migrations/`. Each migration is applied transactionally.

### `help`

Displays available commands:

```
> help
Available commands:
  database update    Run pending migrations
  help               Show this help message
  exit               Exit the CLI
```

### `exit` or `quit`

Exits the CLI.

```
> exit
Goodbye!
```

## Configuration

The CLI reads configuration from two sources (in order):

1. **appsettings.shared.json** (solution root) — shared across all projects
   - Database host, port, name, username
   
2. **appsettings.json** (CLI directory) — project-specific overrides (optional)

3. **Environment variables** — overrides all above

The database password comes from the **POSTGRES_PASSWORD** OS-level environment variable, ensuring it stays synchronized with Aspire's PostgreSQL container and the CLI's migrations.

## Architecture

The CLI delegates migration logic to [`HomeSteadier.Database.DatabaseMigrationService`](../HomeSteadier.Database/DatabaseMigrationService.cs), making migrations executable from other contexts (e.g., the API on startup) without CLI dependencies. For standalone migration execution, import `HomeSteadier.Database` and call:

```csharp
var service = new DatabaseMigrationService();
var result = await service.RunMigrationsAsync(connectionString);
```
