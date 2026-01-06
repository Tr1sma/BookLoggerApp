using System.IO.Compression;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Security;

/// <summary>
/// A mock file system for testing ImportExportService without hitting the disk.
/// </summary>
public class MockFileSystem : IFileSystem
{
    // Minimal implementation for the test
    public bool FileExists(string path) => true;
    public bool DirectoryExists(string path) => true;
    public void CreateDirectory(string path) { }
    public void CopyFile(string source, string dest, bool overwrite) { }
    public string CombinePath(params string[] paths) => Path.Combine(paths);
}

/// <summary>
/// A mock app settings provider.
/// </summary>
public class MockAppSettingsProvider : IAppSettingsProvider
{
    public AppSettings GetSettings() => new AppSettings();
    public Task<AppSettings> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(new AppSettings());
    public Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
    public void InvalidateCache() { }
}

public class ZipSlipTests
{
    [Fact]
    public async Task RestoreFromBackupAsync_ShouldThrowIOException_OnZipSlip()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "ZipSlipTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, "malicious.zip");

        try
        {
            // 1. Create a malicious zip file
            // We can't use the standard ZipFile.CreateFromDirectory to easily create ".." entries
            // because it sanitizes them. We must manipulate the archive directly.
            using (var fileStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                // Create an entry with ".." in the name
                // Note: Windows and some libraries might sanitize this automatically,
                // but this is the standard way to attempt a creation of such an entry for testing.
                var entry = archive.CreateEntry("../../evil.txt");
                using (var entryStream = entry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    writer.Write("This is a malicious file.");
                }
            }

            // 2. Setup Service
            var dbName = Guid.NewGuid().ToString();
            var contextFactory = new TestDbContextFactory(dbName);
            var fileSystem = new MockFileSystem();
            var settingsProvider = new MockAppSettingsProvider();

            // Pass the tempDir as appDataPath so backups/restores happen there
            var service = new ImportExportService(
                contextFactory,
                fileSystem,
                settingsProvider,
                null,
                tempDir);

            // Act & Assert
            // The service should detect the ".." in the entry name and throw an IOException
            await Assert.ThrowsAsync<IOException>(async () =>
            {
                await service.RestoreFromBackupAsync(zipPath);
            });
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
