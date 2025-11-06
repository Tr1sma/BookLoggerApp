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

    // Rating Statistics (Multi-Category)
    /// <summary>
    /// Gets the average rating for a specific category across all rated books.
    /// </summary>
    Task<double> GetAverageRatingByCategoryAsync(RatingCategory category, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);

    /// <summary>
    /// Gets average ratings for all categories.
    /// </summary>
    Task<Dictionary<RatingCategory, double>> GetAllAverageRatingsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the top rated books, optionally filtered by a specific rating category.
    /// </summary>
    Task<List<BookRatingSummary>> GetTopRatedBooksAsync(int count = 10, RatingCategory? category = null, CancellationToken ct = default);

    /// <summary>
    /// Gets all books with their rating summaries.
    /// </summary>
    Task<List<BookRatingSummary>> GetBooksWithRatingsAsync(CancellationToken ct = default);
}
