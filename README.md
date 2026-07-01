# HomeSteadier

A distributed application built with .NET Aspire, featuring a .NET Core API backend, React TypeScript frontend, and PostgreSQL database.

## Project Structure

- **Homesteadier.API** — ASP.NET Core API with OpenAPI/Swagger support
- **HomeSteadier.AppHost** — .NET Aspire orchestration host for managing services
- **HomeSteadier.Models** — Shared domain models and entities (class library)
- **HomeSteadier.ServiceDefaults** — Shared service configuration and defaults
- **ReactApp** — React frontend (TypeScript + Vite)

## Getting Started

### Prerequisites

- **.NET 10.0 SDK** or later
- **Docker** running (required for PostgreSQL and Aspire services)
- **Node.js 18+** (for React development)

### Environment Configuration

Before running the application, set the PostgreSQL database password as a machine-level environment variable. This password is used by both Aspire (to initialize the PostgreSQL container) and the CLI (to run database migrations).

#### Windows

```powershell
setx POSTGRES_PASSWORD "your-secure-password"
```

Then **restart your terminal or IDE** for the environment variable to take effect.

**Note:** After setting the variable with `setx`, you must close and fully reopen your terminal/IDE window (not just a new tab) for the change to be picked up.

#### macOS/Linux

```bash
export POSTGRES_PASSWORD="your-secure-password"
```

To persist it across sessions, add the line above to your shell profile (`~/.bashrc`, `~/.zshrc`, etc.):

```bash
echo 'export POSTGRES_PASSWORD="your-secure-password"' >> ~/.bashrc
```

### Run the Application

#### Option 1: Using the Batch Script (Recommended for Windows)

Run the included batch script which handles building and running the Aspire host:

```bash
run-aspire.bat
```

#### Option 2: Manual Command Line

```bash
dotnet run --project HomeSteadier.AppHost
```

Once started, the Aspire Dashboard will be available (check console output for the URL), and:
- **API**: Available via the URL shown in the dashboard
- **React Frontend**: Automatically served and accessible through your browser
- **PostgreSQL**: Running in Docker with persistent storage

### Windows Terminal Profiles

To create Windows Terminal tabs that auto-launch the app or CLI:

#### Apphost Profile

1. Open **Windows Terminal Settings** (Ctrl+,)
2. Go to **Profiles** → **+ New Profile**
3. Configure:
   - **Name**: `HomeSteadier Apphost`
   - **Command Line**: `cmd.exe /k "run-aspire.bat"`
   - **Starting Directory**: `directory where the repo is located`
4. Click **Save**

#### CLI Profile

1. Open **Windows Terminal Settings** (Ctrl+,)
2. Go to **Profiles** → **+ New Profile**
3. Configure:
   - **Name**: `HomeSteadier CLI`
   - **Command Line**: `cmd.exe /k "run-cli.bat"`
   - **Starting Directory**: `directory where the repo is located`
4. Click **Save**

Now you can launch either the app or the CLI directly from the terminal profile dropdown.

## Development

### API Development

The API is located in `Homesteadier.API/` and includes:
- Swagger/OpenAPI documentation at `/swagger`

### React Development

The React app is located in `ReactApp/` and includes:
- Vite dev server with hot module replacement
- TypeScript support
- Configured to communicate with the API via environment variables

To work on the frontend while the Aspire host is running, you can also run:

```bash
cd ReactApp
npm run dev
```

## Documentation

- [CLI Documentation](docs/CLI.md) — Admin tool for local development
- [Aspire Troubleshooting Guide](docs/ASPIRE-TROUBLESHOOTING.md) — Solutions for common Aspire and Docker issues
