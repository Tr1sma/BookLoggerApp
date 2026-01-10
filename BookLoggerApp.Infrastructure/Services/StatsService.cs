using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

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
        // Optimized: Calculate sum in database to avoid loading all completed books into memory.
        return await _unitOfWork.Context.Set<Book>()
            .Where(b => b.Status == ReadingStatus.Completed && b.PageCount.HasValue)
            .SumAsync(b => b.PageCount!.Value, ct);
    }

    public async Task<int> GetTotalMinutesReadAsync(CancellationToken ct = default)
    {
        // Optimized: Calculate sum in database using existing repository method.
        return await _unitOfWork.ReadingSessions.GetTotalMinutesAsync(ct);
    }

    public async Task<int> GetCurrentStreakAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var allSessions = await _unitOfWork.ReadingSessions.GetAllAsync();

        var sessionsByDate = allSessions
            .GroupBy(s => s.StartedAt.Date)
            .OrderByDescending(g => g.Key)
            .ToList();

        if (!sessionsByDate.Any())
            return 0;

        var mostRecentDate = sessionsByDate.First().Key;
        if ((today - mostRecentDate).Days > 1)
            return 0;

        int streak = 0;
        var currentDate = today;

        foreach (var group in sessionsByDate)
        {
            if ((currentDate - group.Key).Days <= 1)
            {
                streak++;
                currentDate = group.Key;
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    public async Task<int> GetLongestStreakAsync(CancellationToken ct = default)
    {
        var allSessions = await _unitOfWork.ReadingSessions.GetAllAsync();
        var sessionDates = allSessions
            .Select(s => s.StartedAt.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (!sessionDates.Any())
            return 0;

        int longestStreak = 1;
        int currentStreak = 1;

        for (int i = 1; i < sessionDates.Count; i++)
        {
            if ((sessionDates[i] - sessionDates[i - 1]).Days == 1)
            {
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else if ((sessionDates[i] - sessionDates[i - 1]).Days > 1)
            {
                currentStreak = 1;
            }
        }

        return longestStreak;
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
        // Optimized: Calculate count in database to avoid loading all completed books.
        return await _unitOfWork.Context.Set<Book>()
            .AsNoTracking()
            .Where(b => b.Status == ReadingStatus.Completed && b.DateCompleted.HasValue && b.DateCompleted.Value.Year == year)
            .CountAsync(ct);
    }

    public async Task<Dictionary<string, int>> GetBooksByGenreAsync(CancellationToken ct = default)
    {
        // Optimized: Group by genre directly in the database to avoid loading all books and genres into memory.
        var genreCounts = await _unitOfWork.Context.Set<BookGenre>()
            .GroupBy(bg => bg.Genre.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Count, ct);

        return genreCounts;
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
        // Optimized: Calculate average in database to avoid loading all completed books.
        var query = _unitOfWork.Context.Set<Book>()
            .AsNoTracking()
            .Where(b => b.Status == ReadingStatus.Completed);

        if (startDate.HasValue)
        {
            query = query.Where(b => b.DateCompleted.HasValue && b.DateCompleted.Value >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(b => b.DateCompleted.HasValue && b.DateCompleted.Value <= endDate.Value);
        }

        IQueryable<int?> ratingQuery = category switch
        {
            RatingCategory.Characters => query.Select(b => b.CharactersRating),
            RatingCategory.Plot => query.Select(b => b.PlotRating),
            RatingCategory.WritingStyle => query.Select(b => b.WritingStyleRating),
            RatingCategory.SpiceLevel => query.Select(b => b.SpiceLevelRating),
            RatingCategory.Pacing => query.Select(b => b.PacingRating),
            RatingCategory.WorldBuilding => query.Select(b => b.WorldBuildingRating),
            _ => null
        };

        if (ratingQuery == null)
            return 0;

        return (await ratingQuery.AverageAsync(ct)) ?? 0;
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
            sortedBooks = category.Value switch
            {
                RatingCategory.Characters => books.Where(b => b.CharactersRating.HasValue).OrderByDescending(b => b.CharactersRating),
                RatingCategory.Plot => books.Where(b => b.PlotRating.HasValue).OrderByDescending(b => b.PlotRating),
                RatingCategory.WritingStyle => books.Where(b => b.WritingStyleRating.HasValue).OrderByDescending(b => b.WritingStyleRating),
                RatingCategory.SpiceLevel => books.Where(b => b.SpiceLevelRating.HasValue).OrderByDescending(b => b.SpiceLevelRating),
                RatingCategory.Pacing => books.Where(b => b.PacingRating.HasValue).OrderByDescending(b => b.PacingRating),
                RatingCategory.WorldBuilding => books.Where(b => b.WorldBuildingRating.HasValue).OrderByDescending(b => b.WorldBuildingRating),
                _ => Enumerable.Empty<Book>()
            };
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

}
