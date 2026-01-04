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
        var completedBooks = await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed);
        return completedBooks.Where(b => b.PageCount.HasValue).Sum(b => b.PageCount!.Value);
    }

    public async Task<int> GetTotalMinutesReadAsync(CancellationToken ct = default)
    {
        var allSessions = await _unitOfWork.ReadingSessions.GetAllAsync();
        return allSessions.Sum(s => s.Minutes);
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
        var books = await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed);
        return books.Count(b => b.DateCompleted.HasValue && b.DateCompleted.Value.Year == year);
    }

    public async Task<Dictionary<string, int>> GetBooksByGenreAsync(CancellationToken ct = default)
    {
        var books = await _unitOfWork.Context.Books
            .Include(b => b.BookGenres)
            .ThenInclude(bg => bg.Genre)
            .ToListAsync();

        return books
            .SelectMany(b => b.BookGenres.Select(bg => bg.Genre.Name))
            .GroupBy(name => name)
            .ToDictionary(g => g.Key, g => g.Count());
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
        var ratings = GetRatingsForCategory(books, category);
        var ratingList = ratings.ToList();
        return ratingList.Any() ? ratingList.Average() : 0;
    }

    public async Task<Dictionary<RatingCategory, double>> GetAllAverageRatingsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        // ⚡ Bolt Optimization: Fetch books once instead of 6 times
        // This reduces database queries from N (number of categories) to 1
        var books = await GetFilteredBooksAsync(startDate, endDate, ct);
        var result = new Dictionary<RatingCategory, double>();

        foreach (RatingCategory category in Enum.GetValues(typeof(RatingCategory)))
        {
            var ratings = GetRatingsForCategory(books, category);
            var ratingList = ratings.ToList();
            result[category] = ratingList.Any() ? ratingList.Average() : 0;
        }

        return result;
    }

    private IEnumerable<int> GetRatingsForCategory(IEnumerable<Book> books, RatingCategory category)
    {
        return category switch
        {
            RatingCategory.Characters => books.Where(b => b.CharactersRating.HasValue).Select(b => b.CharactersRating!.Value),
            RatingCategory.Plot => books.Where(b => b.PlotRating.HasValue).Select(b => b.PlotRating!.Value),
            RatingCategory.WritingStyle => books.Where(b => b.WritingStyleRating.HasValue).Select(b => b.WritingStyleRating!.Value),
            RatingCategory.SpiceLevel => books.Where(b => b.SpiceLevelRating.HasValue).Select(b => b.SpiceLevelRating!.Value),
            RatingCategory.Pacing => books.Where(b => b.PacingRating.HasValue).Select(b => b.PacingRating!.Value),
            RatingCategory.WorldBuilding => books.Where(b => b.WorldBuildingRating.HasValue).Select(b => b.WorldBuildingRating!.Value),
            _ => Enumerable.Empty<int>()
        };
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
