namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for saving files using platform-specific file pickers.
/// </summary>
public interface IFileSaverService
{
    /// <summary>
    /// Saves content to a file using the platform's file save dialog.
    /// </summary>
    /// <param name="defaultFileName">The suggested file name.</param>
    /// <param name="content">The content to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The path where the file was saved, or null if cancelled.</returns>
    Task<string?> SaveFileAsync(string defaultFileName, string content, CancellationToken ct = default);
}
