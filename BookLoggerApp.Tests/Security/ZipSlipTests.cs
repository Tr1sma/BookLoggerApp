using System.IO.Compression;

using BookLoggerApp.Core.Models;
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
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => _files.ContainsKey(path);
    public bool DirectoryExists(string path) => _directories.Contains(path);
    public void CreateDirectory(string path) => _directories.Add(path);

    public void CopyFile(string source, string dest, bool overwrite)
    {
        if (!_files.TryGetValue(source, out var content))
        {
            throw new FileNotFoundException("Source file not found", source);
        }

        if (!overwrite && _files.ContainsKey(dest))
        {
            throw new IOException($"Destination file already exists: {dest}");
        }

        _files[dest] = content.ToArray();
    }

    public string CombinePath(params string[] paths) => Path.Combine(paths);

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(path, out var content))
        {
            throw new FileNotFoundException("File not found", path);
        }

        return Task.FromResult(System.Text.Encoding.UTF8.GetString(content));
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
    {
        _files[path] = System.Text.Encoding.UTF8.GetBytes(content);
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)
    {
        if (!_files.TryGetValue(path, out var content))
        {
            throw new FileNotFoundException("File not found", path);
        }

        return Task.FromResult(content.ToArray());
    }

    public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct = default)
    {
        _files[path] = content.ToArray();
        return Task.CompletedTask;
    }

    public void DeleteFile(string path)
    {
        _files.Remove(path);
    }

    public Stream OpenWrite(string path)
    {
        var stream = new MemoryStream();
        return new DelegatingWriteStream(stream, bytes => _files[path] = bytes.ToArray());
    }

    private sealed class DelegatingWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly Action<byte[]> _onDispose;

        public DelegatingWriteStream(Stream inner, Action<byte[]> onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Flush();
                _inner.Position = 0;
                using var copy = new MemoryStream();
                _inner.CopyTo(copy);
                _onDispose(copy.ToArray());
            }

            _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}

/// <summary>
/// A mock app settings provider.
/// </summary>
public class MockAppSettingsProvider : IAppSettingsProvider
{
    public event EventHandler? ProgressionChanged;
    public event EventHandler? SettingsChanged;

    private AppSettings _settings = new();
    private int _coins;
    private int _level = 1;
    private int _plantsPurchased;

    public Task<AppSettings> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(_settings);

    public Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    public void InvalidateCache() { }

    public Task<int> GetUserCoinsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_coins);
    }

    public Task<int> GetUserLevelAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_level);
    }

    public Task SpendCoinsAsync(int amount, CancellationToken ct = default)
    {
        _coins = Math.Max(0, _coins - Math.Max(0, amount));
        ProgressionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task AddCoinsAsync(int amount, CancellationToken ct = default)
    {
        _coins += Math.Max(0, amount);
        ProgressionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task IncrementPlantsPurchasedAsync(CancellationToken ct = default)
    {
        _plantsPurchased++;
        return Task.CompletedTask;
    }

    public Task<int> GetPlantsPurchasedAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_plantsPurchased);
    }
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
