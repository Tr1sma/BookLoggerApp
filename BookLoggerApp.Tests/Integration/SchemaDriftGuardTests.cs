using System.Data;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace BookLoggerApp.Tests.Integration;

/// <summary>
/// Verifies the schema drift guard repairs a SQLite DB whose <c>__EFMigrationsHistory</c>
/// claims V10 migrations are applied but whose <c>AppSettings</c> table is missing the
/// corresponding columns — the exact production scenario behind the
/// "no such column: a.AnalyticsEnabled" report.
/// </summary>
public sealed class SchemaDriftGuardTests : IDisposable
{
    private readonly string _dbPath;

    public SchemaDriftGuardTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"booklogger_drift_{Guid.NewGuid():N}.db3");
    }

    public void Dispose()
    {
        // SqliteConnection pools connections; clear them before deleting so Windows
        // doesn't hold the file.
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Tests are cleaning up a temp dir; don't fail the run over a locked file.
        }
    }

    [Fact]
    public async Task EnsureCriticalColumnsAsync_AddsMissingV10Columns_WhenSchemaIsDrifted()
    {
        // Arrange — create a DB with a minimal AppSettings table that looks like
        // the pre-V10 shape: Id + TotalXp present, but the V10 consent/subscription
        // columns absent. __EFMigrationsHistory claims the V10 migrations applied.
        CreateDriftedAppSettingsDb(_dbPath);

        await using var context = CreateContext(_dbPath);

        // Act
        await SchemaDriftGuard.EnsureCriticalColumnsAsync(context, crashReporting: null, logger: null);

        // Assert — every V10 column the guard knows about now exists on the table.
        var columns = await ReadColumnsAsync(_dbPath, "AppSettings");
        columns.Should().Contain("AnalyticsEnabled");
        columns.Should().Contain("CrashReportingEnabled");
        columns.Should().Contain("PrivacyBannerDismissed");
        columns.Should().Contain("PrivacyPolicyAcceptedAt");
        columns.Should().Contain("CurrentTier");
        columns.Should().Contain("EntitlementExpiresAt");
        columns.Should().Contain("HideGettingStartedCta");

        // Existing row kept its data and picked up the defaults for the new columns.
        await using var verifyConn = new SqliteConnection($"Data Source={_dbPath}");
        await verifyConn.OpenAsync();
        await using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = "SELECT AnalyticsEnabled, CrashReportingEnabled, CurrentTier, TotalXp FROM AppSettings;";
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt64(0).Should().Be(1, "AnalyticsEnabled default is true (1) per AppSettings.cs");
        reader.GetInt64(1).Should().Be(1, "CrashReportingEnabled default is true (1)");
        reader.GetInt64(2).Should().Be(0, "CurrentTier default is SubscriptionTier.Free (0)");
        reader.GetInt64(3).Should().Be(42, "pre-existing TotalXp must be preserved");
    }

    [Fact]
    public async Task EnsureCriticalColumnsAsync_IsIdempotent_WhenCalledTwice()
    {
        // Arrange — drifted DB, run the guard once to repair.
        CreateDriftedAppSettingsDb(_dbPath);
        await using (var context1 = CreateContext(_dbPath))
        {
            await SchemaDriftGuard.EnsureCriticalColumnsAsync(context1, crashReporting: null, logger: null);
        }

        var columnsAfterFirstRun = await ReadColumnsAsync(_dbPath, "AppSettings");

        // Act — run the guard a second time on an already-repaired DB.
        await using (var context2 = CreateContext(_dbPath))
        {
            await SchemaDriftGuard.EnsureCriticalColumnsAsync(context2, crashReporting: null, logger: null);
        }

        // Assert — no duplicate columns added, no exceptions thrown.
        var columnsAfterSecondRun = await ReadColumnsAsync(_dbPath, "AppSettings");
        columnsAfterSecondRun.Should().BeEquivalentTo(columnsAfterFirstRun);
    }

    [Fact]
    public async Task AppSettingsProvider_RecoversAutomatically_WhenGetSettingsHitsDrift()
    {
        // Arrange — drifted DB, then insert a full AppSettings row so FirstOrDefaultAsync
        // has something to return after the guard repairs the schema.
        CreateDriftedAppSettingsDb(_dbPath);
        var factory = new FileBackedDbContextFactory(_dbPath);
        var provider = new AppSettingsProvider(factory);

        // Act
        var settings = await provider.GetSettingsAsync();

        // Assert — recovery pulled the existing row and the newly-defaulted columns.
        settings.Should().NotBeNull();
        settings.AnalyticsEnabled.Should().BeTrue();
        settings.CrashReportingEnabled.Should().BeTrue();
        settings.PrivacyBannerDismissed.Should().BeFalse();
        settings.TotalXp.Should().Be(42);
    }

    /// <summary>
    /// Creates a SQLite file with an <c>AppSettings</c> table that has every pre-V10
    /// column the model requires, one row of real data, and <c>__EFMigrationsHistory</c>
    /// rows claiming every shipped migration — including the V10 migrations — has been
    /// applied. This is the exact state observed on users' devices in the field.
    /// </summary>
    private static void CreateDriftedAppSettingsDb(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );

                CREATE TABLE "AppSettings" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AppSettings" PRIMARY KEY,
                    "Theme" TEXT NOT NULL,
                    "Language" TEXT NOT NULL,
                    "ShelfLedgeColor" TEXT NOT NULL,
                    "ShelfBaseColor" TEXT NOT NULL,
                    "NotificationsEnabled" INTEGER NOT NULL,
                    "ReadingRemindersEnabled" INTEGER NOT NULL,
                    "ReminderTime" TEXT NULL,
                    "AutoBackupEnabled" INTEGER NOT NULL,
                    "LastBackupDate" TEXT NULL,
                    "TelemetryEnabled" INTEGER NOT NULL,
                    "UserLevel" INTEGER NOT NULL,
                    "TotalXp" INTEGER NOT NULL,
                    "Coins" INTEGER NOT NULL,
                    "PlantsPurchased" INTEGER NOT NULL,
                    "ReviewPromptDisabled" INTEGER NOT NULL,
                    "LastReviewPromptDate" TEXT NULL,
                    "ReviewPromptMonthCount" INTEGER NOT NULL,
                    "HasCompletedOnboarding" INTEGER NOT NULL DEFAULT 0,
                    "OnboardingFlowVersion" INTEGER NOT NULL DEFAULT 0,
                    "OnboardingIntroStatus" INTEGER NOT NULL DEFAULT 0,
                    "OnboardingCurrentStep" INTEGER NOT NULL DEFAULT 0,
                    "OnboardingCompletedAt" TEXT NULL,
                    "OnboardingAutoCompletedForExistingUser" INTEGER NOT NULL DEFAULT 0,
                    "OnboardingTutorialPlantId" TEXT NULL,
                    "OnboardingTutorialPlantNeedsWateringAssist" INTEGER NOT NULL DEFAULT 0,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL,
                    "RowVersion" BLOB NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO "AppSettings"
                ("Id","Theme","Language","ShelfLedgeColor","ShelfBaseColor",
                 "NotificationsEnabled","ReadingRemindersEnabled","AutoBackupEnabled",
                 "TelemetryEnabled","UserLevel","TotalXp","Coins","PlantsPurchased",
                 "ReviewPromptDisabled","ReviewPromptMonthCount","CreatedAt")
                VALUES
                ($id,'Dark','de','#8B7355','#D4A574',
                 0,0,0,
                 0,1,42,100,0,
                 0,0,$createdAt);
                """;
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        // Fake history rows claiming V10 migrations have been applied — matches what we
        // see in the field on broken installs where MigrateAsync returns clean but the
        // column adds never took.
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion") VALUES
                ('20260422065259_AddAnalyticsConsentFields','10.0.6'),
                ('20260422123532_AddPremiumSubscriptionSystem','10.0.6');
                """;
            cmd.ExecuteNonQuery();
        }
    }

    private static AppDbContext CreateContext(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(string path, string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await cmd.ExecuteReaderAsync();
        var nameOrdinal = reader.GetOrdinal("name");
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(nameOrdinal));
        }
        return columns;
    }

    private sealed class FileBackedDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly string _path;

        public FileBackedDbContextFactory(string path)
        {
            _path = path;
        }

        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_path}")
                .Options;
            return new AppDbContext(options);
        }
    }
}
