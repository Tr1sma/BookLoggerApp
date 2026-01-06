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

    public ImportExportServiceZipIntegrationTests()
    {
        // specific temp folder for this test run
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
        public Task<AppSettings> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(new AppSettings());
        public Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetUserCoinsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetUserLevelAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task SpendCoinsAsync(int amount, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddCoinsAsync(int amount, CancellationToken ct = default) => Task.CompletedTask;
        public Task IncrementPlantsPurchasedAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetPlantsPurchasedAsync(CancellationToken ct = default) => Task.FromResult(0);
        public void InvalidateCache() { }
    }

    // Simple IDbContextFactory implementation for real SQLite file
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
        // 1. Setup Source Environment
        var sourceDir = Path.Combine(_tempRoot, "Source");
        Directory.CreateDirectory(sourceDir);
        var sourceDbPath = Path.Combine(sourceDir, "booklogger.db");
        var sourceCoversDir = Path.Combine(sourceDir, "covers");
        Directory.CreateDirectory(sourceCoversDir);

        // a) Create DB with data
        var sourceFactory = new SqliteDbContextFactory(sourceDbPath);
        using (var context = sourceFactory.CreateDbContext())
        {
            context.Database.EnsureCreated();
            context.Books.Add(new Book { Title = "Backup Test Book", CoverImagePath = "covers/test.jpg" });
            await context.SaveChangesAsync();
        }

        // b) Create dummy image
        var imagePath = Path.Combine(sourceCoversDir, "test.jpg");
        await File.WriteAllTextAsync(imagePath, "imagedata");

        // 2. Perform Backup
        // We use FileSystemAdapter but it will work on the passed basePath
        var fileSystem = new FileSystemAdapter(); 
        var service = new ImportExportService(sourceFactory, fileSystem, new TestAppSettingsProvider(), null, sourceDir);

        var backupZipPath = await service.CreateBackupAsync();

        // Verify Backup Exists
        File.Exists(backupZipPath).Should().BeTrue();
        Path.GetExtension(backupZipPath).Should().Be(".zip");

        // 3. Verify Zip Contents (Optional, Restore verifies it implicitly but good primarily)
        using (var archive = ZipFile.OpenRead(backupZipPath))
        {
            archive.Entries.Should().Contain(e => e.FullName == "booklogger.db");
            archive.Entries.Should().Contain(e => e.FullName == "covers/test.jpg" || e.FullName == "covers\\test.jpg");
        }

        // 4. Setup Target Environment (Simulate Fresh Install)
        var targetDir = Path.Combine(_tempRoot, "Target");
        Directory.CreateDirectory(targetDir);
        var targetDbPath = Path.Combine(targetDir, "booklogger.db"); // Empty or non-existent initially

        // To simulate a fresh app start, usually DB is created empty.
        // Let's create an empty DB there first so Restore has something to overwrite (Restore logic usually closes current DB)
        var targetFactory = new SqliteDbContextFactory(targetDbPath);
        using (var context = targetFactory.CreateDbContext())
        {
            context.Database.EnsureCreated();
            context.Books.Count().Should().Be(0);
        }

        // 5. Perform Restore
        var targetService = new ImportExportService(targetFactory, fileSystem, new TestAppSettingsProvider(), null, targetDir);
        await targetService.RestoreFromBackupAsync(backupZipPath);

        // 6. Verify Restore
        // a) Check Data
        using (var context = targetFactory.CreateDbContext())
        {
            var books = await context.Books.ToListAsync();
            books.Should().HaveCount(1);
            books.First().Title.Should().Be("Backup Test Book");
        }

        // b) Check Images
        var targetCoversDir = Path.Combine(targetDir, "covers");
        var restoredImagePath = Path.Combine(targetCoversDir, "test.jpg");
        File.Exists(restoredImagePath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(restoredImagePath);
        content.Should().Be("imagedata");
    }
}
