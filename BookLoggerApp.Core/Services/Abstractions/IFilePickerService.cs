namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Abstraction for platform-specific file picker functionality.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Opens a file picker to select a single file.
    /// </summary>
    /// <param name="pickerTitle">Title of the picker.</param>
    /// <param name="allowedExtensions">List of allowed file extensions (e.g. ".zip", ".db").</param>
    /// <returns>Full path to the selected file, or null if cancelled.</returns>
    Task<string?> PickFileAsync(string pickerTitle, params string[] allowedExtensions);
}
