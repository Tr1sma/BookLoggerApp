using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// End-to-end regression for the field-reported "no such column: u.IsHiddenByEntitlement"
/// error. Drifts a SQLite DB into the exact state observed when
/// <c>20260422123532_AddPremiumSubscriptionSystem</c> was force-marked applied without
/// running its <c>ALTER TABLE Shelves ADD COLUMN IsHiddenByEntitlement</c> statement,
/// then runs <see cref="SchemaDriftGuard"/> and verifies <see cref="ShelfService"/>
/// queries succeed.
/// </summary>
public sealed class ShelfServiceDriftRecoveryTests : IDisposable
{
    private readonly string _dbPath;

    public ShelfServiceDriftRecoveryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"booklogger_shelfdrift_{Guid.NewGuid():N}.db3");
    }

    public void Dispose()
    {
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
            // Best-effort temp cleanup.
        }
    }

    [Fact]
    public async Task GetAllShelvesAsync_Succeeds_AfterSchemaDriftGuardRepair()
    {
        // Arrange — drifted Shelves table missing IsHiddenByEntitlement.
        CreateDriftedShelvesDb(_dbPath);

        var factory = new FileBackedDbContextFactory(_dbPath);

        // Sanity check: querying ShelfService BEFORE the guard runs would throw
        // "no such column: s.IsHiddenByEntitlement" (the bug we're regressing).
        var service = new ShelfService(factory);
        Func<Task> beforeRepair = async () => await service.GetAllShelvesAsync();
        await beforeRepair.Should().ThrowAsync<SqliteException>(
            "the bug is that the column doesn't exist before SchemaDriftGuard runs");

        // Act — run the guard, which is what DbInitializer does on every boot.
        await using (var context = factory.CreateDbContext())
        {
            await SchemaDriftGuard.EnsureCriticalColumnsAsync(context, crashReporting: null, logger: null);
        }

        // Assert — ShelfService now reads cleanly.
        var shelves = await service.GetAllShelvesAsync();
        shelves.Should().NotBeNull();
        shelves.Should().HaveCount(2, "two shelves were inserted in the drifted DB");
        shelves.Should().AllSatisfy(s => s.IsHiddenByEntitlement.Should().BeFalse(
            "all rows default to 0 when the column is added with DEFAULT 0"));
    }

    private static void CreateDriftedShelvesDb(string path)
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

                CREATE TABLE "Shelves" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Shelves" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "AutoSortRule" INTEGER NOT NULL DEFAULT 0,
                    "SortOrder" INTEGER NOT NULL DEFAULT 0,
                    "Icon" TEXT NOT NULL DEFAULT ''
                );
                """;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO "Shelves" ("Id", "Name", "AutoSortRule", "SortOrder", "Icon") VALUES
                ($id1, 'Shelf 1', 0, 0, '📚'),
                ($id2, 'Shelf 2', 0, 1, '📖');
                """;
            cmd.Parameters.AddWithValue("$id1", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$id2", Guid.NewGuid().ToString());
            cmd.ExecuteNonQuery();
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion") VALUES
                ('20260422123532_AddPremiumSubscriptionSystem','force-mark');
                """;
            cmd.ExecuteNonQuery();
        }
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
