using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for generating shareable monthly reading stats card images.
/// </summary>
public interface IReadingStatsCardService
{
    /// <summary>
    /// Generates a PNG image of the monthly reading stats card and returns the file path.
    /// The image is saved to the app's cache directory for sharing.
    /// </summary>
    Task<string> GenerateMonthlyCardAsync(MonthlyReadingStats stats, CancellationToken ct = default);
}
