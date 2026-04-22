using System.Data;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Repairs schema drift where <c>__EFMigrationsHistory</c> claims a migration is applied
/// but the corresponding columns are absent from the table. Observed in the field on
/// V10 upgrades where <c>AnalyticsEnabled</c> and other columns were missing even after
/// <c>MigrateAsync()</c> ran without throwing — most likely caused by a crashed prior
/// migration or <see cref="DatabaseMigrationHelper"/> swapping in an older DB file whose
/// history table was out of sync with its actual schema.
///
/// Runs after <c>MigrateAsync()</c> in <see cref="DbInitializer"/> and also as a
/// last-chance recovery inside <c>AppSettingsProvider</c> when a "no such column"
/// error surfaces despite the startup guard. Idempotent — every missing column add is
/// wrapped in its own try/catch so a duplicate column doesn't abort the repair loop.
/// </summary>
public static class SchemaDriftGuard
{
    /// <summary>
    /// Columns we know new installs produce that could be missing on drifted DBs.
    /// Keep ordered by migration-introduction date; the defaults mirror
    /// <see cref="BookLoggerApp.Infrastructure.Data.Configurations.AppSettingsConfiguration"/>.
    /// </summary>
    private static readonly IReadOnlyList<ExpectedColumn> ExpectedAppSettingsColumns = new[]
    {
        // V10 — AddHideGettingStartedCta (defensive: users skipping multiple versions hit this too)
        new ExpectedColumn("HideGettingStartedCta",            "INTEGER NOT NULL DEFAULT 0"),
        // V10 — AddAnalyticsConsentFields (the reported error's source)
        new ExpectedColumn("AnalyticsEnabled",                 "INTEGER NOT NULL DEFAULT 1"),
        new ExpectedColumn("CrashReportingEnabled",            "INTEGER NOT NULL DEFAULT 1"),
        new ExpectedColumn("PrivacyBannerDismissed",           "INTEGER NOT NULL DEFAULT 0"),
        new ExpectedColumn("PrivacyPolicyAcceptedAt",          "TEXT NULL"),
        // V10 — AddPremiumSubscriptionSystem
        new ExpectedColumn("CurrentTier",                      "INTEGER NOT NULL DEFAULT 0"),
        new ExpectedColumn("EntitlementExpiresAt",             "TEXT NULL"),
    };

    /// <summary>
    /// Ensures every column the current model expects on the <c>AppSettings</c> table exists
    /// in the SQLite DB, adding any missing column via raw <c>ALTER TABLE</c>. Any successful
    /// repair is reported to Crashlytics as a non-fatal so we can see drift frequency in the
    /// field. The method never throws — the caller (startup) should not crash when the guard
    /// itself has a problem; downstream defensive recovery picks up the slack.
    /// </summary>
    public static async Task EnsureCriticalColumnsAsync(
        AppDbContext context,
        ICrashReportingService? crashReporting,
        ILogger? logger,
        CancellationToken ct = default)
    {
        try
        {
            var existingColumns = await GetExistingColumnsAsync(context, "AppSettings", ct);
            if (existingColumns.Count == 0)
            {
                // Table itself is missing. MigrateAsync should have created it — if it didn't,
                // this guard can't fix it (CREATE TABLE with full schema is the migration's job).
                logger?.LogWarning("SchemaDriftGuard: AppSettings table not found; skipping column repair.");
                return;
            }

            foreach (var column in ExpectedAppSettingsColumns)
            {
                if (existingColumns.Contains(column.Name))
                {
                    continue;
                }

                await TryAddColumnAsync(context, "AppSettings", column, crashReporting, logger, ct);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "SchemaDriftGuard: unexpected error while ensuring AppSettings columns");
            try
            {
                crashReporting?.RecordNonFatal(ex, new Dictionary<string, string>
                {
                    ["source"] = "schema_drift_guard",
                    ["phase"] = "outer"
                });
            }
            catch
            {
                // Reporting failures must not replace the original error.
            }
        }
    }

    private static async Task<HashSet<string>> GetExistingColumnsAsync(
        AppDbContext context,
        string tableName,
        CancellationToken ct)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            // PRAGMA does not support parameter binding in SQLite — the table name must be
            // inlined. All callers pass compile-time constants, so no injection surface.
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

            await using var reader = await command.ExecuteReaderAsync(ct);
            var nameOrdinal = reader.GetOrdinal("name");
            while (await reader.ReadAsync(ct))
            {
                result.Add(reader.GetString(nameOrdinal));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return result;
    }

    private static async Task TryAddColumnAsync(
        AppDbContext context,
        string tableName,
        ExpectedColumn column,
        ICrashReportingService? crashReporting,
        ILogger? logger,
        CancellationToken ct)
    {
        var sql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{column.Name}\" {column.Definition};";
        try
        {
            await context.Database.ExecuteSqlRawAsync(sql, ct);
            logger?.LogWarning(
                "SchemaDriftGuard: repaired drifted schema by adding {Table}.{Column}",
                tableName, column.Name);

            try
            {
                crashReporting?.RecordNonFatal(
                    new InvalidOperationException($"SchemaDrift: added {tableName}.{column.Name}"),
                    new Dictionary<string, string>
                    {
                        ["source"] = "schema_drift_guard",
                        ["table"] = tableName,
                        ["column"] = column.Name
                    });
            }
            catch
            {
                // Reporting failures must not mask the repair success.
            }
        }
        catch (Exception ex)
        {
            // A "duplicate column" here likely means a concurrent init already repaired it —
            // log and move on rather than aborting the remaining columns.
            logger?.LogError(
                ex,
                "SchemaDriftGuard: ALTER TABLE failed for {Table}.{Column}; continuing",
                tableName, column.Name);
        }
    }

    private sealed record ExpectedColumn(string Name, string Definition);
}
