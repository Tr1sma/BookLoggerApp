using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for importing and exporting data in various formats.
/// </summary>
public interface IImportExportService
{
    /// <summary>
    /// Exports all books to JSON format.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON string containing all books and related data.</returns>
    Task<string> ExportToJsonAsync(CancellationToken ct = default);

    /// <summary>
    /// Exports all books to CSV format.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>CSV string containing all books (flattened data).</returns>
    Task<string> ExportToCsvAsync(CancellationToken ct = default);

    /// <summary>
    /// Imports books from JSON format.
    /// </summary>
    /// <param name="json">JSON string containing books data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of books imported.</returns>
    Task<int> ImportFromJsonAsync(string json, CancellationToken ct = default);

    /// <summary>
    /// Imports books from CSV format.
    /// </summary>
    /// <param name="csv">CSV string containing books data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of books imported.</returns>
    Task<int> ImportFromCsvAsync(string csv, CancellationToken ct = default);

    /// <summary>
    /// Creates a full backup of the database.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full path to the backup file.</returns>
    Task<string> CreateBackupAsync(CancellationToken ct = default);

    /// <summary>
    /// Restores the database from a backup file.
    /// </summary>
    /// <param name="backupPath">Full path to the backup file.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RestoreFromBackupAsync(string backupPath, CancellationToken ct = default);
}
