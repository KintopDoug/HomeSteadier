# HomeSteadier CLI Guide

The HomeSteadier CLI provides a command-line interface for managing database migrations and other administrative tasks.

## Prerequisites

- **.NET 10.0 SDK** or later
- **dotnet-ef** global tool installed (required for `dotnet gen` scaffolding):
  ```bash
  dotnet tool install --global dotnet-ef
  ```
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

### `dotnet gen` (or `gen`)

Scaffolds entity models and the `DbContext` from the live PostgreSQL schema using EF Core's database-first scaffolding (`dotnet ef dbcontext scaffold`), then generates a repository interface/implementation for any entity that doesn't already have one.

```
> dotnet gen
Generating entity models from database schema using EF Core scaffolding...
Generating models to: <solution root>\HomeSteadier.Models\Database
Generating repositories to: <solution root>\Homesteadier.Repository\Repositories
Running scaffolding from: <solution root>\Homesteadier.Repository

Skipped: Migration.cs (DbUp-managed table)
Moved User.cs to models directory
Generated: IUserRepository.cs
Generated: UserRepository.cs

Successfully generated 1 model(s), 1 repository/repositories, and DbContext!
```

What happens on each run:

- **Entity models** are scaffolded and placed in `HomeSteadier.Models/Database/`.
- **`Homesteadier.Repository/HomesteadierDbContext.cs`** is regenerated from scratch each time, so it always matches the current database schema.
- **The `migrations` table is always skipped** — it's owned by DbUp, not EF Core, so no model is generated for it and it's stripped out of the DbContext.
- **Repositories are only generated for new entities.** If `I{Entity}Repository.cs` or `{Entity}Repository.cs` already exists in `Homesteadier.Repository/Repositories/`, it's left untouched (and reported as skipped) so any custom query methods you've added aren't overwritten.
- Generated repository implementations carry an `[AutoRegister]` attribute, so the API automatically registers them for dependency injection at startup — no manual wiring needed in `Program.cs`.

### `help`

Displays available commands:

```
> help
Available commands:
  database update    Run pending DbUp migrations
  dotnet gen         Scaffold entity models + DbContext from the database schema,
                     then generate repositories for any new entities
                     (existing repositories are skipped, not overwritten)
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

`dotnet gen` is implemented in [`HomeSteadier.CLI.Services.DotnetService`](../HomeSteadier.CLI/Services/DotnetService.cs), which:

1. Invokes `dotnet ef dbcontext scaffold` against `Homesteadier.Repository`, writing entity models into an isolated temp folder (so pre-existing project files are never mistaken for scaffolded output) and the `DbContext` directly into place.
2. Moves the scaffolded entity models into `HomeSteadier.Models/Database/`, skipping (and deleting) any model for the DbUp-managed `migrations` table.
3. Strips references to skipped entities out of the generated `HomesteadierDbContext.cs` so it still compiles.
4. Generates a repository interface/implementation pair for each entity that doesn't already have one, using the `Homesteadier.Repository.Repository<T>` / `IRepository<T>` base types.

Because scaffolding fully regenerates `HomesteadierDbContext.cs` on every run, don't hand-edit that file — put custom query logic in the generated repository classes instead, which are preserved across runs.
