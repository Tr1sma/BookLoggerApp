using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Production implementation of IFileSystem that wraps System.IO operations.
/// </summary>
public class FileSystemAdapter : IFileSystem
{
    public async Task<string> ReadAllTextAsync(string path, CancellationToken ct = default)
    {
        return await File.ReadAllTextAsync(path, ct);
    }

    public async Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
    {
        await File.WriteAllTextAsync(path, content, ct);
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)
    {
        return await File.ReadAllBytesAsync(path, ct);
    }

    public async Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct = default)
    {
        await File.WriteAllBytesAsync(path, content, ct);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string CombinePath(params string[] paths)
    {
        return Path.Combine(paths);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    public Stream OpenWrite(string path)
    {
        return new FileStream(path, FileMode.Create, FileAccess.Write);
    }
}
