using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for calculating reading statistics and trends.
/// </summary>
public interface IStatsService
{
    // Overall Statistics
    Task<int> GetTotalBooksReadAsync(CancellationToken ct = default);
    Task<int> GetTotalPagesReadAsync(CancellationToken ct = default);
    Task<int> GetTotalMinutesReadAsync(CancellationToken ct = default);
    Task<int> GetCurrentStreakAsync(CancellationToken ct = default);
    Task<int> GetLongestStreakAsync(CancellationToken ct = default);

    // Time-based Statistics
    Task<Dictionary<DateTime, int>> GetReadingTrendAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<int> GetPagesReadInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<int> GetBooksCompletedInYearAsync(int year, CancellationToken ct = default);

    // Genre Statistics
    Task<Dictionary<string, int>> GetBooksByGenreAsync(CancellationToken ct = default);
    Task<string?> GetFavoriteGenreAsync(CancellationToken ct = default);

    // Averages
    Task<double> GetAverageRatingAsync(CancellationToken ct = default);
    Task<double> GetAveragePagesPerDayAsync(int days = 30, CancellationToken ct = default);
    Task<double> GetAverageMinutesPerDayAsync(int days = 30, CancellationToken ct = default);
}
