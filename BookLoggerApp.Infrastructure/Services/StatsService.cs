using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for calculating reading statistics.
/// </summary>
public class StatsService : IStatsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _timeZone;

    // Reading trend and streak group sessions by the user's local calendar day, not raw UTC
    // (CODE_REVIEW LOG-02/LOG-08). The zone is injectable (default TimeZoneInfo.Local) so the
    // grouping stays deterministically testable on any CI offset.
    public StatsService(IUnitOfWork unitOfWork, TimeZoneInfo? timeZone = null)
    {
        _unitOfWork = unitOfWork;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public async Task<int> GetTotalBooksReadAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.Books.CountAsync(b => b.Status == ReadingStatus.Completed, ct);
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

    public Task<int> GetCurrentStreakAsync(CancellationToken ct = default)
        => GetCurrentStreakAsync(DateTime.UtcNow, ct);

    // Internal clock seam so the local-day streak logic stays deterministically testable
    // with a fixed "now" (the public method passes DateTime.UtcNow).
    internal async Task<int> GetCurrentStreakAsync(DateTime utcNow, CancellationToken ct)
    {
        // LOG-02: anchor "today" and each session's day to the user's local calendar so streaks
        // share the goal feature's local-midnight convention instead of raw UTC boundaries.
        var localToday = LocalTimeHelper.LocalDate(utcNow, _timeZone);

        // Load only the last year of sessions; a streak >365 days is unrealistic and this avoids
        // loading thousands of records for long-time users.
        var recentSessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(utcNow.AddDays(-365), utcNow, ct);

        return ReadingStreakHelper.CalculateCurrentStreak(recentSessions, localToday, _timeZone);
    }

    public async Task<int> GetLongestStreakAsync(CancellationToken ct = default)
    {
        var allSessions = await _unitOfWork.ReadingSessions.GetAllAsync(ct);
        // LOG-02: bucket by the user's local calendar day, like GetCurrentStreakAsync.
        return ReadingStreakHelper.CalculateLongestStreak(allSessions, _timeZone);
    }

    public async Task<Dictionary<DateTime, int>> GetReadingTrendAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end, ct);

        // LOG-08: group by the user's local calendar day, not the raw UTC StartedAt.
        return sessions
            .GroupBy(s => LocalTimeHelper.LocalDate(s.StartedAt, _timeZone))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public async Task<int> GetPagesReadInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end, ct);
        return sessions.Where(s => s.PagesRead.HasValue).Sum(s => s.PagesRead!.Value);
    }

    public async Task<int> GetBooksCompletedInYearAsync(int year, CancellationToken ct = default)
    {
        // Optimized: Calculate count in database to avoid loading all completed books into memory.
        return await _unitOfWork.Books.GetCountByCompletionYearAsync(year, ct);
    }

    public async Task<int> GetBooksCompletedInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        return await _unitOfWork.Context.Set<Book>()
            .Where(b => b.Status == ReadingStatus.Completed
                     && b.DateCompleted.HasValue
                     && b.DateCompleted.Value >= start
                     && b.DateCompleted.Value <= end)
            .CountAsync(ct);
    }

    public async Task<List<Book>> GetTopBooksInRangeAsync(DateTime start, DateTime end, int count = 3, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Context.Set<Book>()
            .AsNoTracking()
            .Where(b => b.Status == ReadingStatus.Completed
                     && b.DateCompleted.HasValue
                     && b.DateCompleted.Value >= start
                     && b.DateCompleted.Value <= end)
            .ToListAsync(ct);

        // AverageRating is a computed property — sort in memory after loading.
        // Books with no ratings are sorted last (null-safe descending).
        return books
            .OrderByDescending(b => b.AverageRating ?? -1)
            .Take(count)
            .ToList();
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
        var genreStats = await GetBooksByGenreAsync(ct);

        if (!genreStats.Any())
            return null;

        return genreStats.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    public async Task<double> GetAverageRatingAsync(CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync(ct);
        var ratedBooks = books.Where(b => b.AverageRating.HasValue).ToList();

        if (!ratedBooks.Any())
            return 0;

        return ratedBooks.Average(b => b.AverageRating!.Value);
    }

    public async Task<double> GetAveragePagesPerDayAsync(int days = 30, CancellationToken ct = default)
    {
        if (days <= 0)
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be greater than zero.");

        var start = DateTime.UtcNow.AddDays(-days);
        var end = DateTime.UtcNow;

        var totalPages = await GetPagesReadInRangeAsync(start, end, ct);
        return (double)totalPages / days;
    }

    public async Task<double> GetAverageMinutesPerDayAsync(int days = 30, CancellationToken ct = default)
    {
        if (days <= 0)
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be greater than zero.");

        var start = DateTime.UtcNow.AddDays(-days);
        var end = DateTime.UtcNow;

        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end, ct);
        var totalMinutes = sessions.Sum(s => s.Minutes);

        return (double)totalMinutes / days;
    }

    public async Task<double> GetAverageRatingByCategoryAsync(RatingCategory category, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        // Optimized: Calculate average directly in database to avoid loading all completed books
        return await _unitOfWork.Books.GetAverageRatingByCategoryAsync(category, startDate, endDate, ct);
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
        var books = await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed, ct);

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
                RatingCategory.Spannung => books.Where(b => b.SpannungRating.HasValue).OrderByDescending(b => b.SpannungRating),
                RatingCategory.Humor => books.Where(b => b.HumorRating.HasValue).OrderByDescending(b => b.HumorRating),
                RatingCategory.Informationsgehalt => books.Where(b => b.InformationsgehaltRating.HasValue).OrderByDescending(b => b.InformationsgehaltRating),
                RatingCategory.EmotionaleTiefe => books.Where(b => b.EmotionaleTiefeRating.HasValue).OrderByDescending(b => b.EmotionaleTiefeRating),
                RatingCategory.Atmosphaere => books.Where(b => b.AtmosphaereRating.HasValue).OrderByDescending(b => b.AtmosphaereRating),
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
        // Z.761: only completed books are summarised — filter status DB-side instead of loading
        // the whole library and filtering in memory.
        var books = await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed, ct);

        return books
            .Select(BookRatingSummary.FromBook)
            .OrderByDescending(s => s.AverageRating)
            .ToList();
    }

    public async Task<List<(int Year, int Month)>> GetActiveReadingPeriodsAsync(CancellationToken ct = default)
    {
        var dates = await _unitOfWork.Context.Set<Book>()
            .Where(b => b.Status == ReadingStatus.Completed && b.DateCompleted.HasValue)
            .Select(b => b.DateCompleted!.Value)
            .ToListAsync(ct);

        return dates
            .Select(d => (d.Year, d.Month))
            .Distinct()
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .ToList();
    }

}
