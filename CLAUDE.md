# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

HomeSteadier: a .NET Aspire-orchestrated distributed app — ASP.NET Core API, React/TypeScript SPA, PostgreSQL — plus an interactive CLI for codegen and migrations.

## Environment setup (required before running anything)

Two machine-level environment variables are required, both read via `Process` → `User` → `Machine` fallback (so a `setx` needs a fresh terminal/IDE to be picked up):

- `POSTGRES_PASSWORD` — used by Aspire (to init the Postgres container), the API (to build its connection string), and the CLI (migrations).
- `JWT_SIGNING_KEY` — must be 32+ characters (HS256 requires ≥32 bytes). The API fails fast at startup if missing or too short.

Docker must be running (Postgres runs in a container via Aspire).

## Common commands

### Run the whole stack (Aspire)
```bash
dotnet run --project HomeSteadier.AppHost
```
or `run-aspire.bat` on Windows. This starts Postgres (Docker), the API, and the React dev server together, wired up via Aspire service discovery/env vars (e.g. `VITE_API_URL` is injected into the React app from the API's endpoint).

### Run the API standalone
```bash
dotnet run --project Homesteadier.API
```
Swagger UI is at `/swagger` in Development. The API runs DbUp migrations automatically on startup before accepting requests.

### React app (ReactApp/)
```bash
npm run dev       # Vite dev server
npm run build     # tsc -b && vite build
npm run lint      # eslint .
```

### CLI (interactive REPL for codegen + migrations)
```bash
dotnet run --project HomeSteadier.CLI
```
or `run-cli.bat`. Commands inside the REPL (see [docs/CLI.md](docs/CLI.md) for full detail):
- `database update` — runs pending DbUp SQL migrations from `HomeSteadier.Database/Migrations/`.
- `database gen` — regenerates `CREATE TABLE` snapshot scripts in `HomeSteadier.Database/Tables/` from the live schema (read-only snapshot, not applied).
- `dotnet gen` (or `gen`) — scaffolds EF Core entity models + `HomesteadierDbContext` from the live Postgres schema, and generates a repository interface/impl for any entity that doesn't already have one.
- `packages gen` — generates TypeScript request/response models under `ReactApp/src/models/` and axios-based API client classes under `ReactApp/src/api/` (one per controller, e.g. `UsersApi.tsx` exporting `UsersApi.getAll()`), both from the API's live OpenAPI document (requires the API running; defaults to `http://localhost:5128`, override via `Api:BaseUrl` / `Api__BaseUrl`).

`dotnet-ef` global tool is required for `dotnet gen`: `dotnet tool install --global dotnet-ef`.

## Architecture

### Codegen-driven backend workflow

This is the most important thing to understand before touching the database layer. The normal flow when adding/changing a table is:

1. Write a new DbUp SQL migration in `HomeSteadier.Database/Migrations/`, then run `database update` in the CLI.
2. Run `dotnet gen` in the CLI. This re-scaffolds **everything** in `HomesteadierDbContext.cs` and the entity models in `HomeSteadier.Models/Database/` from the live schema — **never hand-edit `HomesteadierDbContext.cs` or scaffolded model files**, they're overwritten every run.
3. For any newly-scaffolded entity, `dotnet gen` also generates `I{Entity}Repository`/`{Entity}Repository` in `Homesteadier.Repository/Repositories/` — but only if they don't already exist, so hand-written query methods on existing repositories are safe and preserved. Put custom query logic there.
4. Repositories carry `[AutoRegister]` ([Homesteadier.Repository/AutoRegisterAttribute.cs](Homesteadier.Repository/AutoRegisterAttribute.cs)); `Homesteadier.API/Program.cs` reflects over the repository assembly at startup and registers every `[AutoRegister]`-marked class against its `I*Repository` interface automatically — no manual DI wiring needed for new repositories.
5. Run `packages gen` (with the API running) to regenerate matching TypeScript request/response models in `ReactApp/src/models/`, plus an axios API client class per controller in `ReactApp/src/api/`. These are also fully regenerated each run — don't hand-edit them.

The `migrations` table itself (DbUp's journal table) is always excluded from `dotnet gen`/`database gen` output — it's DbUp-owned, not application data.

### Solution structure

- **Homesteadier.API** — ASP.NET Core Web API. Controllers, JWT auth (`Auth/`), CORS, and startup wiring live here. This is the only project that composes everything else together.
- **HomeSteadier.AppHost** — .NET Aspire orchestration ([AppHost.cs](HomeSteadier.AppHost/AppHost.cs)): declares the Postgres container (`pgvector/pgvector` image), the API project, and the React app as a `JavaScriptApp`, and wires the API's endpoint into the React app's env as `VITE_API_URL`.
- **HomeSteadier.CLI** — interactive REPL for the codegen/migration commands above.
- **HomeSteadier.Database** — migration/schema-generation logic, referenced by both the API (to auto-run migrations on startup) and the CLI (`DatabaseMigrationService`, `TableScriptGenerationService`), so migration logic isn't duplicated or CLI-dependent.
- **HomeSteadier.Migrations** — the DbUp SQL migration scripts themselves (embedded resources), applied transactionally and tracked in the `migrations` table.
- **HomeSteadier.Models** — shared domain models: `Database/` (EF entities, scaffolded), `Request/` and `Response/` (API DTOs, hand-written, organized by feature e.g. `Request/Auth/`).
- **Homesteadier.Repository** — EF Core `DbContext` (scaffolded) + repository pattern. `Repository<T>` / `IRepository<T>` give basic CRUD; concrete repositories extend `Repository<T>` and add custom queries. Also holds the custom ASP.NET Identity `UserStore` ([Identity/UserStore.cs](Homesteadier.Repository/Identity/UserStore.cs)) that adapts Identity onto the existing `users` table rather than Identity's default schema.
- **HomeSteadier.ServiceDefaults** — shared Aspire service-defaults (telemetry, health checks, service discovery) added via `builder.AddServiceDefaults()` in both API and AppHost.
- **ReactApp** — Vite + TypeScript + React 19 SPA. Uses TanStack Router (file-based route tree generated into `src/routeTree.gen.ts` — don't hand-edit) and MobX for state.

### Config

`appsettings.shared.json` at the solution root is the single source of shared config (DB name/host/port, JWT issuer/audience/expiry, refresh-cookie settings, CORS allowed origins). Both `AppHost.cs` and `Homesteadier.API/Program.cs` locate it at runtime by walking up from `AppContext.BaseDirectory` looking for `HomeSteadier.slnx` (`FindSolutionRoot`), so it works regardless of which project is launched from. Secrets (`POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`) are deliberately kept out of this file and sourced from the environment instead.

### Auth

JWT bearer auth issued by `Homesteadier.API/Auth/JwtTokenService.cs`, validated via `AddJwtBearer` in `Program.cs` with `MapInboundClaims = false` (claims like `sub` are kept as-is rather than remapped to legacy XML URIs). Refresh tokens are stored server-side (`Repositories/RefreshTokenRepository.cs`) and handed to the client as an httpOnly cookie (`RefreshTokenCookie.cs`), which is why CORS is configured with an explicit origin allow-list + `AllowCredentials()` rather than a wildcard (wildcard origins can't carry credentials).

### React app patterns

- Components are arrow functions wrapped in `observer(...)` from `mobx-react-lite`, not class components or `function Foo()` declarations.
- State lives in MobX view models (`src/viewModels/`), constructed with `makeAutoObservable(this, {}, { autoBind: true })`, not `useState`/decorators. A page component creates its view model with `useMemo` and calls an `initialize()` method (see [SignUpViewModel.ts](ReactApp/src/viewModels/SignUpViewModel.ts) and [signUp.tsx](ReactApp/src/pages/signUp.tsx) for the pattern).
- `src/routeTree.gen.ts` is generated by the TanStack Router Vite plugin from files under `src/routes/` — don't hand-edit it.
- `src/models/request/` and `src/models/response/` are generated by the CLI's `packages gen` from the API's OpenAPI schema, organized to mirror the API's DTO folders — don't hand-edit these either.
- `src/api/{Controller}Api.tsx` (e.g. `UsersApi.tsx`) files are also generated by `packages gen`, one per controller — each exports a singleton client (e.g. `UsersApi`) with one instance method per endpoint, named after the controller action and camelCased (e.g. `UsersApi.getAll()`). Don't hand-edit these. `src/api/httpClient.ts` is the shared axios instance they all call through — it's the one file in that folder that *is* hand-written and safe to edit (e.g. to add auth-header interceptors).
