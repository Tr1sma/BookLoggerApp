using System.Text.RegularExpressions;
using BookLoggerApp.Core.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Helpers for recovering from migration failures where the schema element a migration
/// would create already exists in the DB. Shared between <see cref="DbInitializer"/>
/// (regular boot path) and <see cref="Services.ImportExportService"/> (restore path)
/// so both surfaces handle the same edge cases the same way.
/// </summary>
internal static class MigrationRecovery
{
    /// <summary>
    /// Matches the canonical SQLite "<c>&lt;object&gt; already exists</c>" DDL phrasings —
    /// <c>table</c>/<c>index</c>/<c>trigger</c>/<c>view</c> — and nothing else. A bare
    /// "already exists" substring is intentionally NOT enough: unrelated errors (e.g.
    /// "a file with that name already exists") must not be mistaken for a recoverable
    /// schema-already-applied condition. See code review LOG-03.
    /// </summary>
    private static readonly Regex SchemaObjectAlreadyExistsRegex = new(
        @"\b(table|index|trigger|view)\b.*\balready exists\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// True when <paramref name="ex"/> indicates the SQL DDL operation failed because
    /// the schema element it tried to create already exists. Covers the SQLite messages
    /// observed in field reports — "duplicate column name" and "<c>table/index/trigger/view
    /// ... already exists</c>" — all of which are recoverable by treating the migration as
    /// already applied. The match is deliberately narrow (LOG-03): a generic "already
    /// exists" substring from an unrelated failure must not trigger a force-mark, which
    /// would skip the migration's remaining, genuinely-needed statements.
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
    /// Deletes any rows from <c>__EFMigrationsLock</c>. EF Core 9's
    /// <c>SqliteHistoryRepository</c> uses this table for migration locking and polls
    /// it via <c>INSERT OR IGNORE</c> until the existing row is gone. If a previous
    /// migration attempt crashed (or the user force-closed the app mid-migration), the
    /// lock row sticks around and every subsequent app start polls forever waiting
    /// for it. We're a single-process Android app, so any pre-existing lock is by
    /// definition stale.
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
            // Fresh DB or pre-EF-9 DB: the table doesn't exist. EF Core will create
            // it as part of MigrateAsync's lock acquisition.
            DatabaseInitializationHelper.AppendInitLog(
                "  [lock] no __EFMigrationsLock table yet");
        }
        catch (Exception ex)
        {
            // Don't fail boot for a lock-cleanup failure; worst case is the next
            // attempt polls again. Log so we know.
            DatabaseInitializationHelper.AppendInitLog(
                $"  [lock] could not clear: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Inserts a row into <c>__EFMigrationsHistory</c> for <paramref name="migrationId"/>
    /// so EF Core stops considering it pending. Used as recovery after the migration's
    /// SQL was rejected because the schema it would create already exists. Idempotent
    /// via <c>INSERT OR IGNORE</c> — falls through silently if the row is already there.
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
