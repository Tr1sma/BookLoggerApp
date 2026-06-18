using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using Xunit;

namespace BookLoggerApp.Tests.Integration;

public class ImportExportServiceZipIntegrationTests : IDisposable
{
    private readonly string _tempRoot;

    static ImportExportServiceZipIntegrationTests()
    {
        // RestoreFromBackupAsync gates on DatabaseInitializationHelper.EnsureInitializedAsync
        // to avoid racing the fire-and-forget DbInitializer on fresh installs. Tests bypass
        // the initializer entirely, so satisfy the gate eagerly and idempotently here.
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
    }

    public ImportExportServiceZipIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"BookLoggerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, true); } catch { /* ignore locks */ }
        }
    }

    private class TestAppSettingsProvider : IAppSettingsProvider
    {
        public event EventHandler? ProgressionChanged;
        public event EventHandler? SettingsChanged;
        public Task<AppSettings> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(new AppSettings());
        public Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetUserCoinsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetUserLevelAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task SpendCoinsAsync(int amount, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddCoinsAsync(int amount, CancellationToken ct = default) => Task.CompletedTask;
        public Task IncrementPlantsPurchasedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetPlantsPurchasedAsync(CancellationToken ct = default) => Task.FromResult(0);
        public void InvalidateCache() { }
        public void InvalidateCache(bool notifyProgressionChanged) { }
    }

    private class SqliteDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly string _connectionString;

        public SqliteDbContextFactory(string dbPath)
        {
            _connectionString = $"Data Source={dbPath};Pooling=false";
        }

        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connectionString)
                .Options;

            return new AppDbContext(options);
        }
    }

    [Fact]
    public async Task BackupAndRestore_ShouldPersistDbAndImages()
    {
        var sourceDir = Path.Combine(_tempRoot, "Source");
        Directory.CreateDirectory(sourceDir);
        var sourceDbPath = Path.Combine(sourceDir, "booklogger.db");
        var sourceCoversDir = Path.Combine(sourceDir, "covers");
        Directory.CreateDirectory(sourceCoversDir);

        // use Migrate so __EFMigrationsHistory is populated
        var sourceFactory = new SqliteDbContextFactory(sourceDbPath);
        using (var context = sourceFactory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
            context.Books.Add(new Book { Title = "Backup Test Book", CoverImagePath = "covers/test.jpg" });
            await context.SaveChangesAsync();
        }

        var imagePath = Path.Combine(sourceCoversDir, "test.jpg");
        await File.WriteAllTextAsync(imagePath, "imagedata");

        var fileSystem = new FileSystemAdapter();
        var service = new ImportExportService(sourceFactory, fileSystem, new TestAppSettingsProvider(), null, sourceDir);

        var backupZipPath = await service.CreateBackupAsync();

        File.Exists(backupZipPath).Should().BeTrue();
        Path.GetExtension(backupZipPath).Should().Be(".zip");

        using (var archive = ZipFile.OpenRead(backupZipPath))
        {
            archive.Entries.Should().Contain(e => e.FullName == "booklogger.db");
            archive.Entries.Should().Contain(e => e.FullName == "covers/test.jpg" || e.FullName == "covers\\test.jpg");
        }

        var targetDir = Path.Combine(_tempRoot, "Target");
        Directory.CreateDirectory(targetDir);
        var targetDbPath = Path.Combine(targetDir, "booklogger.db");

        var targetFactory = new SqliteDbContextFactory(targetDbPath);
        using (var context = targetFactory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
            context.Books.Count().Should().Be(0);
        }

        var targetService = new ImportExportService(targetFactory, fileSystem, new TestAppSettingsProvider(), null, targetDir);
        await targetService.RestoreFromBackupAsync(backupZipPath);

        using (var context = targetFactory.CreateDbContext())
        {
            var books = await context.Books.ToListAsync();
            books.Should().HaveCount(1);
            books.First().Title.Should().Be("Backup Test Book");
        }

        var targetCoversDir = Path.Combine(targetDir, "covers");
        var restoredImagePath = Path.Combine(targetCoversDir, "test.jpg");
        File.Exists(restoredImagePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(restoredImagePath);
        content.Should().Be("imagedata");
    }

    [Fact]
    public async Task RestoreFromBackupAsync_ShouldHandleCaseInsensitiveBackupEntries()
    {
        var sourceDir = Path.Combine(_tempRoot, "CaseInsensitiveSource");
        Directory.CreateDirectory(sourceDir);
        var sourceDbPath = Path.Combine(sourceDir, "booklogger.db");
        var sourceFactory = new SqliteDbContextFactory(sourceDbPath);
        using (var sourceContext = sourceFactory.CreateDbContext())
        {
            await sourceContext.Database.MigrateAsync();
            sourceContext.Books.Add(new Book { Title = "Case Test Book" });
            await sourceContext.SaveChangesAsync();
        }

        var backupPath = Path.Combine(sourceDir, "case-insensitive.zip");
        using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
        {
            var dbEntry = archive.CreateEntry("BOOKLOGGER.DB");
            await using (var dbStream = dbEntry.Open())
            await using (var sourceDbStream = File.OpenRead(sourceDbPath))
            {
                await sourceDbStream.CopyToAsync(dbStream);
            }

            var coverEntry = archive.CreateEntry("COVERS/TEST.JPG");
            await using (var coverStream = coverEntry.Open())
            await using (var writer = new StreamWriter(coverStream))
            {
                await writer.WriteAsync("imagedata");
            }
        }

        var targetDir = Path.Combine(_tempRoot, "CaseInsensitiveTarget");
        Directory.CreateDirectory(targetDir);
        var targetDbPath = Path.Combine(targetDir, "booklogger.db");

        var targetFactory = new SqliteDbContextFactory(targetDbPath);
        using (var context = targetFactory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var service = new ImportExportService(targetFactory, new FileSystemAdapter(), new TestAppSettingsProvider(), null, targetDir);
        await service.RestoreFromBackupAsync(backupPath);

        using (var context = targetFactory.CreateDbContext())
        {
            (await context.Books.CountAsync()).Should().Be(1);
        }

        File.Exists(Path.Combine(targetDir, "covers", "TEST.JPG")).Should().BeTrue();
    }
}
