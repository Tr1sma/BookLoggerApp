using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for calculating reading statistics.
/// </summary>
public class StatsService : IStatsService
{
    private readonly IUnitOfWork _unitOfWork;

    public StatsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> GetTotalBooksReadAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.Books.CountAsync(b => b.Status == ReadingStatus.Completed);
    }

    public async Task<int> GetTotalPagesReadAsync(CancellationToken ct = default)
    {
        var completedBooks = await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed);
        return completedBooks.Where(b => b.PageCount.HasValue).Sum(b => b.PageCount!.Value);
    }

    public async Task<int> GetTotalMinutesReadAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetTotalMinutesAsync(ct);
    }

    public async Task<int> GetCurrentStreakAsync(CancellationToken ct = default)
    {
        var dates = await _unitOfWork.ReadingSessions.GetSessionDatesAsync(ct);
        return StreakCalculator.CalculateCurrentStreak(dates);
    }

    public async Task<int> GetLongestStreakAsync(CancellationToken ct = default)
    {
        var dates = await _unitOfWork.ReadingSessions.GetSessionDatesAsync(ct);
        return StreakCalculator.CalculateLongestStreak(dates);
    }

    public async Task<Dictionary<DateTime, int>> GetReadingTrendAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end);

        return sessions
            .GroupBy(s => s.StartedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public async Task<int> GetPagesReadInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end);
        return sessions.Where(s => s.PagesRead.HasValue).Sum(s => s.PagesRead!.Value);
    }

    public async Task<int> GetBooksCompletedInYearAsync(int year, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed);
        return books.Count(b => b.DateCompleted.HasValue && b.DateCompleted.Value.Year == year);
    }

    public async Task<Dictionary<string, int>> GetBooksByGenreAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.Books.GetGenreStatsAsync();
    }

    public async Task<string?> GetFavoriteGenreAsync(CancellationToken ct = default)
    {
        var genreStats = await GetBooksByGenreAsync();

        if (!genreStats.Any())
            return null;

        return genreStats.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    public async Task<double> GetAverageRatingAsync(CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync();
        var ratedBooks = books.Where(b => b.AverageRating.HasValue).ToList();

        if (!ratedBooks.Any())
            return 0;

        return ratedBooks.Average(b => b.AverageRating!.Value);
    }

    public async Task<double> GetAveragePagesPerDayAsync(int days = 30, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow.AddDays(-days);
        var end = DateTime.UtcNow;

        var totalPages = await GetPagesReadInRangeAsync(start, end);
        return (double)totalPages / days;
    }

    public async Task<double> GetAverageMinutesPerDayAsync(int days = 30, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow.AddDays(-days);
        var end = DateTime.UtcNow;

        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end);
        var totalMinutes = sessions.Sum(s => s.Minutes);

        return (double)totalMinutes / days;
    }

    public async Task<double> GetAverageRatingByCategoryAsync(RatingCategory category, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var books = await GetFilteredBooksAsync(startDate, endDate, ct);

        var ratings = books
            .Select(b => RatingHelper.GetRating(b, category))
            .Where(r => r.HasValue)
            .Select(r => r!.Value);

        var ratingList = ratings.ToList();
        return ratingList.Any() ? ratingList.Average() : 0;
    }

    public async Task<Dictionary<RatingCategory, double>> GetAllAverageRatingsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var result = new Dictionary<RatingCategory, double>();

        foreach (RatingCategory category in Enum.GetValues(typeof(RatingCategory)))
        {
            var average = await GetAverageRatingByCategoryAsync(category, startDate, endDate, ct);
            result[category] = average;
        }

        return result;
    }

    public async Task<List<BookRatingSummary>> GetTopRatedBooksAsync(int count = 10, RatingCategory? category = null, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed);

        IEnumerable<Book> sortedBooks;

        if (category.HasValue)
        {
            // Sort by specific category
            sortedBooks = books
                .Where(b => RatingHelper.GetRating(b, category.Value).HasValue)
                .OrderByDescending(b => RatingHelper.GetRating(b, category.Value));
        }
        else
        {
            // Sort by average rating
            sortedBooks = books
                .Where(b => b.AverageRating.HasValue)
                .OrderByDescending(b => b.AverageRating ?? 0);
        }

        return sortedBooks
            .Take(count)
            .Select(BookRatingSummary.FromBook)
            .ToList();
    }

    public async Task<List<BookRatingSummary>> GetBooksWithRatingsAsync(CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync();

        return books
            .Where(b => b.Status == ReadingStatus.Completed)
            .Select(BookRatingSummary.FromBook)
            .OrderByDescending(s => s.AverageRating)
            .ToList();
    }

    /// <summary>
    /// Helper method to filter books by date range.
    /// </summary>
    private async Task<List<Book>> GetFilteredBooksAsync(DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
    {
        var books = (await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed)).ToList();

        if (startDate.HasValue)
        {
            books = books.Where(b => b.DateCompleted.HasValue && b.DateCompleted.Value >= startDate.Value).ToList();
        }

        if (endDate.HasValue)
        {
            books = books.Where(b => b.DateCompleted.HasValue && b.DateCompleted.Value <= endDate.Value).ToList();
        }

        return books;
    }
}
