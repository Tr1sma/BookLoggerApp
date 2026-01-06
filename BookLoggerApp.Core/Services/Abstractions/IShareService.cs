namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Abstraction for platform-specific share functionality.
/// </summary>
public interface IShareService
{
    /// <summary>
    /// Shares a file with other applications.
    /// </summary>
    /// <param name="title">Title of the share request.</param>
    /// <param name="filePath">Full path to the file to share.</param>
    /// <param name="contentType">MIME type of the file (optional). Default is derived from extension.</param>
    Task ShareFileAsync(string title, string filePath, string contentType = "application/octet-stream");
}
