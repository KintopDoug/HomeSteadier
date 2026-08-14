using Microsoft.Extensions.Logging;
using Npgsql;

namespace HomeSteadier.Database;

/// <summary>
/// Runs one-shot database initialization at application startup: pending DbUp migrations
/// (fail-fast) followed by reference-data seeding (warn-and-continue). The whole run is serialized
/// across processes and replicas by a session-level Postgres advisory lock, so concurrent API
/// replicas — e.g. overlapping revisions during an ACA rollout, or a scale-up — don't race on the
/// migration journal or seed upserts. The first replica performs the work; the rest block briefly
/// on the lock and then find nothing to do (migrations are journaled, seeds are idempotent upserts).
/// </summary>
public class DatabaseInitializer
{
    // Arbitrary fixed key. Every process must use the same value to contend on the same lock.
    private const long AdvisoryLockKey = 0x486F6D6553746472; // "HomeStdr"

    private readonly DatabaseMigrationService _migrationService = new();
    private readonly SeedDataService _seedDataService = new();

    public async Task InitializeAsync(string connectionString, string seedsPath, ILogger logger)
    {
        // Dedicated connection holds the session-level advisory lock for the whole run. Session
        // locks are released on explicit unlock or when the connection closes, so even a fail-fast
        // migration (which throws below) still frees the lock via the await-using dispose.
        await using var lockConnection = new NpgsqlConnection(connectionString);
        await lockConnection.OpenAsync();

        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", lockConnection))
        {
            acquire.Parameters.AddWithValue("key", AdvisoryLockKey);
            await acquire.ExecuteNonQueryAsync();
        }

        try
        {
            // Migrations: fail-fast. A bad migration must abort startup rather than serve a
            // half-migrated schema.
            var migrationResult = await _migrationService.RunMigrationsAsync(connectionString);
            if (!migrationResult.Success)
            {
                logger.LogError("Database migration failed: {Error}", migrationResult.Error);
                throw new InvalidOperationException($"Database migration failed: {migrationResult.Error}");
            }

            logger.LogInformation("Database migrations completed successfully");

            // Seeds: warn-and-continue. Missing or stale reference data shouldn't take the app down.
            var seedResult = await _seedDataService.SeedAsync(connectionString, seedsPath);
            if (seedResult.Success)
            {
                var seeded = seedResult.Succeeded.Count > 0
                    ? string.Join(", ", seedResult.Succeeded.Select(s => $"{s.Table} ({s.RowsImported} row(s))"))
                    : "nothing to seed";
                logger.LogInformation("Seed data imported: {Seeded}", seeded);
            }
            else
            {
                var failures = seedResult.Failed.Count > 0
                    ? string.Join("; ", seedResult.Failed.Select(f => $"{f.Table}: {f.Error}"))
                    : seedResult.Error;
                logger.LogWarning(
                    "Seed data import did not fully succeed (continuing startup): {Failures}", failures);
            }
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", lockConnection);
            release.Parameters.AddWithValue("key", AdvisoryLockKey);
            await release.ExecuteNonQueryAsync();
        }
    }
}
