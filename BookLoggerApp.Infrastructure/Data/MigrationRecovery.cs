using System.Text.RegularExpressions;
using BookLoggerApp.Core.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Helpers for recovering from migrations whose schema element already exists. Shared between
/// <see cref="DbInitializer"/> (boot) and <see cref="Services.ImportExportService"/> (restore).
/// </summary>
internal static class MigrationRecovery
{
    /// <summary>
    /// Matches the SQLite "table/index/trigger/view ... already exists" DDL phrasings and nothing
    /// else. A bare "already exists" substring is deliberately NOT enough — unrelated errors must
    /// not be mistaken for a recoverable schema-already-applied condition (LOG-03).
    /// </summary>
    private static readonly Regex SchemaObjectAlreadyExistsRegex = new(
        @"\b(table|index|trigger|view)\b.*\balready exists\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// True when <paramref name="ex"/> indicates the DDL failed because the schema element already
    /// exists ("duplicate column name" or "table/index/trigger/view ... already exists"), which is
    /// recoverable by treating the migration as applied. Deliberately narrow (LOG-03).
    /// </summary>
    public static bool IsSchemaAlreadyAppliedError(Exception ex)
    {
        // Walk the inner-exception chain because EF Core often wraps the original
        // SqliteException in higher-level types.
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var msg = e.Message;
            if (string.IsNullOrEmpty(msg))
            {
                continue;
            }
            if (msg.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase) ||
                SchemaObjectAlreadyExistsRegex.IsMatch(msg))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Deletes any rows from <c>__EFMigrationsLock</c>. EF Core 9 polls this lock via
    /// <c>INSERT OR IGNORE</c> forever if a row survives a crashed migration. For a
    /// single-process Android app any pre-existing lock is by definition stale.
    /// </summary>
    public static async Task ClearStaleMigrationLockAsync(
        DbContext context,
        CancellationToken ct = default)
    {
        try
        {
            var deleted = await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"__EFMigrationsLock\";", ct);
            if (deleted > 0)
            {
                DatabaseInitializationHelper.AppendInitLog(
                    $"  [lock] cleared {deleted} stale __EFMigrationsLock row(s)");
            }
            else
            {
                DatabaseInitializationHelper.AppendInitLog(
                    "  [lock] no stale __EFMigrationsLock rows");
            }
        }
        catch (Exception ex) when (
            ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            // Fresh/pre-EF-9 DB: table doesn't exist yet; MigrateAsync creates it.
            DatabaseInitializationHelper.AppendInitLog(
                "  [lock] no __EFMigrationsLock table yet");
        }
        catch (Exception ex)
        {
            // Don't fail boot for a lock-cleanup failure; worst case the next attempt polls again.
            DatabaseInitializationHelper.AppendInitLog(
                $"  [lock] could not clear: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Inserts a row into <c>__EFMigrationsHistory</c> for <paramref name="migrationId"/> so EF Core
    /// stops considering it pending, after its SQL was rejected for already-existing schema.
    /// Idempotent via <c>INSERT OR IGNORE</c>.
    /// </summary>
    public static async Task ForceMarkMigrationAppliedAsync(
        DbContext context,
        string migrationId,
        CancellationToken ct = default)
    {
        try
        {
            // Synthetic ProductVersion makes it obvious in audits that this row was
            // not written by the standard migration pipeline.
            const string productVersion = "force-mark";
            await context.Database.ExecuteSqlRawAsync(
                "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1});",
                new object[] { migrationId, productVersion },
                ct);
            DatabaseInitializationHelper.AppendInitLog(
                $"    [history] inserted '{migrationId}' into __EFMigrationsHistory (force-mark)");
        }
        catch (Exception ex)
        {
            DatabaseInitializationHelper.AppendInitLog(
                $"    [history] failed to force-mark '{migrationId}': {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}
