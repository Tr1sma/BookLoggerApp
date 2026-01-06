namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Abstraction for file system operations to enable testability.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Reads all text from a file asynchronously.
    /// </summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Writes all text to a file asynchronously.
    /// </summary>
    Task WriteAllTextAsync(string path, string content, CancellationToken ct = default);

    /// <summary>
    /// Reads all bytes from a file asynchronously.
    /// </summary>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Writes all bytes to a file asynchronously.
    /// </summary>
    Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct = default);

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Checks if a directory exists at the specified path.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Combines path segments into a single path.
    /// </summary>
    string CombinePath(params string[] paths);

    /// <summary>
    /// Creates a directory if it doesn't exist.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Deletes a file at the specified path.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Copies a file from source to destination.
    /// </summary>
    void CopyFile(string sourcePath, string destinationPath, bool overwrite = false);

    /// <summary>
    /// Opens a file stream for writing.
    /// </summary>
    Stream OpenWrite(string path);
}
