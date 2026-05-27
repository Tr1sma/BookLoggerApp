using System.Data;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Repairs schema drift where <c>__EFMigrationsHistory</c> claims a migration is applied
/// but the corresponding columns or tables are absent from the SQLite DB. This was
/// originally observed on V10 upgrades where <c>AnalyticsEnabled</c> and other
/// AppSettings columns were missing even after <c>MigrateAsync()</c> ran without
/// throwing — most likely caused by a crashed prior migration that left the history
/// table out of sync with the actual schema.
///
/// In V10.0.6 the same failure mode was observed for the entire
/// <c>20260422123532_AddPremiumSubscriptionSystem</c> migration. When that migration's
/// first <c>ALTER TABLE</c> hits a "duplicate column name" on a partially-applied DB,
/// <see cref="MigrationRecovery.IsSchemaAlreadyAppliedError"/> kicks in and force-marks
/// the migration as applied, but the remaining 8 ALTER TABLEs, the
/// <c>UserEntitlements</c> CREATE TABLE and the seed rows are silently skipped.
/// This guard now repairs every column and table that migration introduces.
///
/// Runs after <c>MigrateAsync()</c> in <see cref="DbInitializer"/> and also as a
/// last-chance recovery inside <c>AppSettingsProvider</c> when a "no such column"
/// error surfaces despite the startup guard. Idempotent — every column/table add is
/// wrapped in its own try/catch so a duplicate column doesn't abort the repair loop.
/// </summary>
public static class SchemaDriftGuard
{
    /// <summary>
    /// Declarative list of every table the guard knows how to repair, with the
    /// columns each must contain and (for tables that an entire migration may have
    /// failed to create) the full <c>CREATE TABLE</c> statement plus an optional
    /// default-row insert.
    ///
    /// IMPORTANT: When <see cref="UserEntitlementsCreateSql"/> changes (i.e. a future
    /// migration alters that table's schema), this list must be updated to match.
    /// See <c>CLAUDE.md</c> "Migration Recovery" section.
    /// </summary>
    private static readonly IReadOnlyList<ExpectedTable> ExpectedTables = new[]
    {
        // --- AppSettings: existing pre-V10 callers expect these to be repaired. ---
        new ExpectedTable(
            Name: "AppSettings",
            CreateTableSql: null,
            Columns: new[]
            {
                // V10 — AddHideGettingStartedCta (defensive: users skipping multiple versions hit this too)
                new ExpectedColumn("HideGettingStartedCta",   "INTEGER NOT NULL DEFAULT 0"),
                // V10 — AddAnalyticsConsentFields (the original drift report's source)
                new ExpectedColumn("AnalyticsEnabled",        "INTEGER NOT NULL DEFAULT 1"),
                new ExpectedColumn("CrashReportingEnabled",   "INTEGER NOT NULL DEFAULT 1"),
                new ExpectedColumn("PrivacyBannerDismissed",  "INTEGER NOT NULL DEFAULT 0"),
                new ExpectedColumn("PrivacyPolicyAcceptedAt", "TEXT NULL"),
                // V10 — AddPremiumSubscriptionSystem
                new ExpectedColumn("CurrentTier",             "INTEGER NOT NULL DEFAULT 0"),
                new ExpectedColumn("EntitlementExpiresAt",    "TEXT NULL"),
            },
            SeedRowSqlIfJustCreated: null),

        // --- AddPremiumSubscriptionSystem: ALTER TABLE columns on existing tables. ---
        new ExpectedTable(
            Name: "UserPlants",
            CreateTableSql: null,
            Columns: new[]
            {
                new ExpectedColumn("IsHiddenByEntitlement", "INTEGER NOT NULL DEFAULT 0"),
            },
            SeedRowSqlIfJustCreated: null),

        new ExpectedTable(
            Name: "UserDecorations",
            CreateTableSql: null,
            Columns: new[]
            {
                new ExpectedColumn("IsHiddenByEntitlement", "INTEGER NOT NULL DEFAULT 0"),
            },
            SeedRowSqlIfJustCreated: null),

        new ExpectedTable(
            Name: "Shelves",
            CreateTableSql: null,
            Columns: new[]
            {
                new ExpectedColumn("IsHiddenByEntitlement", "INTEGER NOT NULL DEFAULT 0"),
            },
            SeedRowSqlIfJustCreated: null),

        new ExpectedTable(
            Name: "ShopItems",
            CreateTableSql: null,
            Columns: new[]
            {
                new ExpectedColumn("IsFreeTier",     "INTEGER NOT NULL DEFAULT 0"),
                new ExpectedColumn("IsUltimateTier", "INTEGER NOT NULL DEFAULT 0"),
            },
            SeedRowSqlIfJustCreated: null),

        new ExpectedTable(
            Name: "PlantSpecies",
            CreateTableSql: null,
            Columns: new[]
            {
                new ExpectedColumn("IsFreeTier",     "INTEGER NOT NULL DEFAULT 0"),
                new ExpectedColumn("IsPrestigeTier", "INTEGER NOT NULL DEFAULT 0"),
            },
            SeedRowSqlIfJustCreated: null),

        // --- AddPremiumSubscriptionSystem: brand-new table that may be missing. ---
        new ExpectedTable(
            Name: "UserEntitlements",
            CreateTableSql: UserEntitlementsCreateSql,
            Columns: Array.Empty<ExpectedColumn>(),
            SeedRowSqlIfJustCreated: UserEntitlementsSeedSql),
    };

    /// <summary>
    /// Mirrors the <c>CREATE TABLE</c> emitted by migration
    /// <c>20260422123532_AddPremiumSubscriptionSystem</c> at lines 76-104. Kept as a
    /// constant so a future <c>UserEntitlements</c> schema change is a single-file
    /// search target — see CLAUDE.md migration section for the maintenance rule.
    /// </summary>
    private const string UserEntitlementsCreateSql = """
        CREATE TABLE IF NOT EXISTS "UserEntitlements" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_UserEntitlements" PRIMARY KEY,
            "Tier" INTEGER NOT NULL DEFAULT 0,
            "BillingPeriod" INTEGER NULL,
            "ProductId" TEXT NULL,
            "PurchaseToken" TEXT NULL,
            "OrderId" TEXT NULL,
            "PurchasedAt" TEXT NULL,
            "ExpiresAt" TEXT NULL,
            "LastVerifiedAt" TEXT NULL,
            "AutoRenewing" INTEGER NOT NULL DEFAULT 0,
            "InGracePeriod" INTEGER NOT NULL DEFAULT 0,
            "IsInIntroductoryPrice" INTEGER NOT NULL DEFAULT 0,
            "IsFamilyShared" INTEGER NOT NULL DEFAULT 0,
            "LapseReason" TEXT NULL,
            "LapsedAt" TEXT NULL,
            "PromoCodeRedeemed" TEXT NULL,
            "PromoExpiresAt" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL,
            "RowVersion" BLOB NULL
        );
        """;

    /// <summary>
    /// Default Free-tier <c>UserEntitlement</c> row matching the <c>InsertData</c>
    /// at migration line 288-291. Only inserted when the guard had to create the
    /// table from scratch — otherwise we trust whatever rows exist.
    /// </summary>
    private const string UserEntitlementsSeedSql = """
        INSERT OR IGNORE INTO "UserEntitlements"
            ("Id", "Tier", "AutoRenewing", "InGracePeriod", "IsInIntroductoryPrice",
             "IsFamilyShared", "CreatedAt")
        VALUES ('99999999-0000-0000-0000-000000000002', 0, 0, 0, 0, 0, '2024-01-01 00:00:00');
        """;

    /// <summary>
    /// Ensures every column/table the current model expects exists in the SQLite DB,
    /// adding any missing column via raw <c>ALTER TABLE</c> and creating any missing
    /// table via raw <c>CREATE TABLE IF NOT EXISTS</c>. Successful repairs are
    /// reported to Crashlytics as non-fatals so drift frequency is observable in the
    /// field. The method never throws — startup must not crash when the guard itself
    /// has a problem; downstream defensive recovery picks up the slack.
    /// </summary>
    public static async Task EnsureCriticalColumnsAsync(
        AppDbContext context,
        ICrashReportingService? crashReporting,
        ILogger? logger,
        CancellationToken ct = default)
    {
        foreach (var table in ExpectedTables)
        {
            try
            {
                await RepairTableAsync(context, table, crashReporting, logger, ct);
            }
            catch (Exception ex)
            {
                // Per-table catch: one failure must not abort the rest of the repair
                // loop (e.g. a transient SQLite error on Shelves shouldn't stop us
                // from fixing UserPlants).
                logger?.LogError(ex, "SchemaDriftGuard: failed to repair {Table}", table.Name);
                SafeReportNonFatal(crashReporting, ex,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["source"] = "schema_drift_guard",
                        ["phase"]  = "table_loop",
                        ["table"]  = table.Name,
                    });
            }
        }
    }

    private static async Task RepairTableAsync(
        AppDbContext context,
        ExpectedTable table,
        ICrashReportingService? crashReporting,
        ILogger? logger,
        CancellationToken ct)
    {
        var existing = await GetExistingColumnsAsync(context, table.Name, ct);
        bool tableJustCreated = false;

        if (existing.Count == 0)
        {
            if (string.IsNullOrEmpty(table.CreateTableSql))
            {
                // Table is supposed to exist via an earlier migration. The guard can't
                // recreate it without the full schema — log and move on.
                logger?.LogWarning(
                    "SchemaDriftGuard: {Table} not found and no CREATE TABLE SQL configured; skipping.",
                    table.Name);
                return;
            }

            await TryCreateTableAsync(context, table, crashReporting, logger, ct);
            tableJustCreated = true;
            existing = await GetExistingColumnsAsync(context, table.Name, ct);
        }

        foreach (var column in table.Columns)
        {
            if (existing.Contains(column.Name))
            {
                continue;
            }

            await TryAddColumnAsync(context, table.Name, column, crashReporting, logger, ct);
        }

        if (tableJustCreated && !string.IsNullOrEmpty(table.SeedRowSqlIfJustCreated))
        {
            await TrySeedAsync(context, table, crashReporting, logger, ct);
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

            SafeReportNonFatal(crashReporting,
                new InvalidOperationException($"SchemaDrift: added {tableName}.{column.Name}"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "schema_drift_guard",
                    ["phase"]  = "add_column",
                    ["table"]  = tableName,
                    ["column"] = column.Name,
                });
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

    private static async Task TryCreateTableAsync(
        AppDbContext context,
        ExpectedTable table,
        ICrashReportingService? crashReporting,
        ILogger? logger,
        CancellationToken ct)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(table.CreateTableSql!, ct);
            logger?.LogWarning(
                "SchemaDriftGuard: repaired drifted schema by creating table {Table}",
                table.Name);

            SafeReportNonFatal(crashReporting,
                new InvalidOperationException($"SchemaDrift: created table {table.Name}"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "schema_drift_guard",
                    ["phase"]  = "create_table",
                    ["table"]  = table.Name,
                });
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "SchemaDriftGuard: CREATE TABLE failed for {Table}; continuing",
                table.Name);
        }
    }

    private static async Task TrySeedAsync(
        AppDbContext context,
        ExpectedTable table,
        ICrashReportingService? crashReporting,
        ILogger? logger,
        CancellationToken ct)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(table.SeedRowSqlIfJustCreated!, ct);
            logger?.LogInformation(
                "SchemaDriftGuard: seeded default row into newly created {Table}",
                table.Name);

            SafeReportNonFatal(crashReporting,
                new InvalidOperationException($"SchemaDrift: seeded {table.Name}"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "schema_drift_guard",
                    ["phase"]  = "seed_row",
                    ["table"]  = table.Name,
                });
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "SchemaDriftGuard: seed insert failed for {Table}; continuing",
                table.Name);
        }
    }

    private static void SafeReportNonFatal(
        ICrashReportingService? crashReporting,
        Exception exception,
        Dictionary<string, string> keys)
    {
        if (crashReporting is null)
        {
            return;
        }

        try
        {
            crashReporting.RecordNonFatal(exception, keys);
        }
        catch
        {
            // Reporting failures must never replace or mask a real schema repair.
        }
    }

    private sealed record ExpectedColumn(string Name, string Definition);

    private sealed record ExpectedTable(
        string Name,
        string? CreateTableSql,
        IReadOnlyList<ExpectedColumn> Columns,
        string? SeedRowSqlIfJustCreated);
}
