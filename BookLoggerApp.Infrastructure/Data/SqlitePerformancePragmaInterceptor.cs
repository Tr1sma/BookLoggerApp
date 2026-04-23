using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Runs SQLite tuning pragmas every time EF opens a connection. WAL is persisted
/// in the database header once set (EF enables it by default), so we reissue it
/// only to be safe; synchronous, temp_store, and cache_size are per-connection
/// and need to be set on every open.
///
/// Rationale: on low-end Android devices with eMMC storage (e.g. Samsung Galaxy
/// A16), fsync latency under the default synchronous=FULL dominates EF Core
/// migration time and every SaveChangesAsync. synchronous=NORMAL keeps
/// transactional durability (writes survive process crashes) while skipping
/// redundant fsyncs on the WAL file, typically speeding up writes 3-10x on slow
/// storage with no safety tradeoff for a user-local app database.
/// </summary>
public sealed class SqlitePerformancePragmaInterceptor : DbConnectionInterceptor
{
    private const string TuningSql =
        "PRAGMA journal_mode=WAL;" +
        "PRAGMA synchronous=NORMAL;" +
        "PRAGMA temp_store=MEMORY;" +
        "PRAGMA cache_size=-8000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ApplyPragmas(connection);
        return Task.CompletedTask;
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = TuningSql;
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Pragma failures are never fatal — the connection still works, just
            // without the speedup. Swallow and keep going rather than take down
            // the whole data layer over a tuning hint.
            System.Diagnostics.Debug.WriteLine($"[SqlitePerformancePragmaInterceptor] PRAGMA tuning failed: {ex.Message}");
        }
    }
}
