using Testcontainers.PostgreSql;

namespace Homesteadier.Tests.Infrastructure;

/// <summary>
/// One real Postgres container and one in-process API for the whole test run.
///
/// The image is the one local dev runs (HomeSteadier.AppHost/AppHost.cs: pgvector/pgvector, tag pg17), so
/// the schema the tests see is what the app really gets. Nothing is mocked: the API starts through its
/// normal path, applies the DbUp migrations and seeds reference data itself, and talks to this database.
///
/// Test isolation is by data, not by teardown: every test creates its own users and farms with unique
/// values and its own client IP, so tests can run in any order and in parallel across classes.
/// </summary>
internal static class SharedStack
{
    private static readonly Lazy<Task<ApiFactory>> Instance =
        new(StartAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task<ApiFactory> GetAsync() => Instance.Value;

    private static async Task<ApiFactory> StartAsync()
    {
        var container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("homesteadier-db")
            .WithUsername("homesteadier_admin")
            .WithPassword("tests-only-throwaway")
            .Build();

        try
        {
            await container.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start the Postgres test container. These tests need Docker running. If Docker is running but "
                + "the error mentions the client API version being too new, your Docker engine is older than the "
                + "Testcontainers client expects: set DOCKER_API_VERSION to the API version `docker version` reports "
                + "for the server (for example 1.41) and run again.",
                ex);
        }

        // Stop the container when the test process exits (Testcontainers' own reaper also covers a crash).
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            container.DisposeAsync().AsTask().GetAwaiter().GetResult();

        // These are process-wide and never restored. That is deliberate and fine because this project is its own
        // test process; do not run other test projects in the same process.
        // Program.cs reads these exactly as it does anywhere else: environment variables win over the shared
        // appsettings.shared.json, and ConnectionStrings:<Database:Name> is what Aspire injects in real runs.
        SetEnv("ConnectionStrings__homesteadier-db", container.GetConnectionString());
        SetEnv("JWT_SIGNING_KEY", "tests-only-signing-key-0123456789abcdef-0123456789");
        // Plain-http TestServer cannot carry the Secure / SameSite=None refresh cookie that production uses.
        SetEnv("RefreshCookie__Secure", "false");
        SetEnv("RefreshCookie__SameSite", "Lax");

        var factory = new ApiFactory();
        // Start the host now so a startup failure (bad migration, bad config) is reported once, clearly,
        // instead of as a confusing failure in whichever test happens to run first.
        _ = factory.Server;
        return factory;
    }

    private static void SetEnv(string name, string value) =>
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
}
