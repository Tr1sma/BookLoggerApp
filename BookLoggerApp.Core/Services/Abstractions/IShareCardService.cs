using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Generates shareable PNG cards for reading stats and book recommendations.
/// </summary>
public interface IShareCardService
{
    /// <summary>
    /// Generates a 1080x1920 Instagram Story-format PNG card for reading stats.
    /// </summary>
    Task<byte[]> GenerateStatsCardAsync(StatsShareData data, CancellationToken ct = default);

    /// <summary>
    /// Generates a book recommendation PNG card.
    /// </summary>
    Task<byte[]> GenerateBookCardAsync(BookShareData data, CancellationToken ct = default);
}
