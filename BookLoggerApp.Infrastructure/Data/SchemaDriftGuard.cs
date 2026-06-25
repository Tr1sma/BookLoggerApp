using System.Data;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Repairs schema drift where <c>__EFMigrationsHistory</c> claims a migration is applied but its
/// columns/tables are absent (observed on V10 upgrades, and for the whole AddPremiumSubscriptionSystem
/// migration in V10.0.6 when a force-mark skipped its remaining statements). Runs after
/// <c>MigrateAsync()</c> in <see cref="DbInitializer"/> and as last-chance recovery in
/// <c>AppSettingsProvider</c>. Idempotent — each add has its own try/catch.
/// </summary>
public static class SchemaDriftGuard
{
    /// <summary>
    /// Declarative list of every table the guard repairs, with required columns and (for tables a
    /// migration may have failed to create) the full <c>CREATE TABLE</c> plus optional seed row.
    /// IMPORTANT: keep in sync when <see cref="UserEntitlementsCreateSql"/> changes — see CLAUDE.md.
    /// </summary>
    private static readonly IReadOnlyList<ExpectedTable> ExpectedTables = new[]
    {
        // AppSettings: pre-V10 callers expect these repaired.
        new ExpectedTable(
            Name: "AppSettings",
            CreateTableSql: null,
            Columns: new[]
            {
                // V10 AddHideGettingStartedCta (defensive: version-skippers hit this too)
                new ExpectedColumn("HideGettingStartedCta",   "INTEGER NOT NULL DEFAULT 0"),
                // V10 AddAnalyticsConsentFields (original drift report source)
                new ExpectedColumn("AnalyticsEnabled",        "INTEGER NOT NULL DEFAULT 1"),
                new ExpectedColumn("CrashReportingEnabled",   "INTEGER NOT NULL DEFAULT 1"),
                new ExpectedColumn("PrivacyBannerDismissed",  "INTEGER NOT NULL DEFAULT 0"),
                new ExpectedColumn("PrivacyPolicyAcceptedAt", "TEXT NULL"),
                // V10 AddPremiumSubscriptionSystem
                new ExpectedColumn("CurrentTier",             "INTEGER NOT NULL DEFAULT 0"),
                new ExpectedColumn("EntitlementExpiresAt",    "TEXT NULL"),
                // AddLiveTimerNotificationSetting
                new ExpectedColumn("LiveTimerNotificationEnabled", "INTEGER NOT NULL DEFAULT 1"),
                // AddSessionMoodTracking
                new ExpectedColumn("MoodTrackingEnabled", "INTEGER NOT NULL DEFAULT 1"),
            },
            SeedRowSqlIfJustCreated: null),

        // AddPremiumSubscriptionSystem: ALTER TABLE columns on existing tables.
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

        // AddPremiumSubscriptionSystem: brand-new table that may be missing.
        new ExpectedTable(
            Name: "UserEntitlements",
            CreateTableSql: UserEntitlementsCreateSql,
            Columns: Array.Empty<ExpectedColumn>(),
            SeedRowSqlIfJustCreated: UserEntitlementsSeedSql),

        // AddSessionMoodTracking: brand-new child table that may be missing.
        new ExpectedTable(
            Name: "ReadingSessionMoods",
            CreateTableSql: ReadingSessionMoodsCreateSql,
            Columns: Array.Empty<ExpectedColumn>(),
            SeedRowSqlIfJustCreated: null),
    };

    /// <summary>
    /// Mirrors the <c>CREATE TABLE</c> from migration AddPremiumSubscriptionSystem. Kept as a
    /// constant so a future schema change is a single-file search target — see CLAUDE.md.
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
    /// Mirrors the <c>CREATE TABLE</c> from migration AddSessionMoodTracking. Kept as a constant
    /// so a future schema change is a single-file search target — see CLAUDE.md.
    /// </summary>
    private const string ReadingSessionMoodsCreateSql = """
        CREATE TABLE IF NOT EXISTS "ReadingSessionMoods" (
            "ReadingSessionId" TEXT NOT NULL,
            "Mood" INTEGER NOT NULL,
            CONSTRAINT "PK_ReadingSessionMoods" PRIMARY KEY ("ReadingSessionId", "Mood"),
            CONSTRAINT "FK_ReadingSessionMoods_ReadingSessions_ReadingSessionId"
                FOREIGN KEY ("ReadingSessionId") REFERENCES "ReadingSessions" ("Id") ON DELETE CASCADE
        );
        """;

    /// <summary>
    /// Default Free-tier <c>UserEntitlement</c> row matching the migration's <c>InsertData</c>.
    /// Only inserted when the guard had to create the table from scratch.
    /// </summary>
    private const string UserEntitlementsSeedSql = """
        INSERT OR IGNORE INTO "UserEntitlements"
            ("Id", "Tier", "AutoRenewing", "InGracePeriod", "IsInIntroductoryPrice",
             "IsFamilyShared", "CreatedAt")
        VALUES ('99999999-0000-0000-0000-000000000002', 0, 0, 0, 0, 0, '2024-01-01 00:00:00');
        """;

    /// <summary>
    /// Ensures every expected column/table exists, adding missing ones via raw
    /// <c>ALTER TABLE</c>/<c>CREATE TABLE IF NOT EXISTS</c>. Repairs are reported to Crashlytics so
    /// drift is observable. Never throws — startup must not crash if the guard itself fails.
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
                // Per-table catch: one failure must not abort the rest of the repair loop.
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

    /// <summary>
    /// Test-only projection of <see cref="ExpectedTables"/> so a drift test can assert every
    /// table/column the guard hard-codes still maps to the current EF model (Z.673). Keeps the
    /// record types private while exposing just the names the assertion needs.
    /// </summary>
    internal static IReadOnlyList<(string Table, IReadOnlyList<string> Columns, bool HasCreateSql)> GetExpectedSchemaForTests()
        => ExpectedTables
            .Select(t => (
                t.Name,
                (IReadOnlyList<string>)t.Columns.Select(c => c.Name).ToList(),
                !string.IsNullOrEmpty(t.CreateTableSql)))
            .ToList();

    private sealed record ExpectedColumn(string Name, string Definition);

    private sealed record ExpectedTable(
        string Name,
        string? CreateTableSql,
        IReadOnlyList<ExpectedColumn> Columns,
        string? SeedRowSqlIfJustCreated);
}
