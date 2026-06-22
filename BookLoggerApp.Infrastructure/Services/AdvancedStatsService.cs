using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for advanced reading statistics (trends and analyses).
///
/// <para>The Stats Trends/Analyses ViewModels fan these methods out concurrently via
/// <c>Task.WhenAll</c>. EF Core forbids concurrent operations on one DbContext, so each
/// method opens its OWN context from an <see cref="IDbContextFactory{AppDbContext}"/>
/// (CODE_REVIEW BUG-06) rather than sharing a single injected UnitOfWork.</para>
/// </summary>
public class AdvancedStatsService : IAdvancedStatsService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly TimeZoneInfo _timeZone;

    // ReadingSession.StartedAt is canonically UTC; day/weekday/hour buckets must be formed in the
    // user's local calendar (CODE_REVIEW LOG-04/LOG-08). The zone is injectable (default
    // TimeZoneInfo.Local) so the bucketing stays deterministically testable on any CI offset.
    public AdvancedStatsService(IDbContextFactory<AppDbContext> contextFactory, TimeZoneInfo? timeZone = null)
    {
        _contextFactory = contextFactory;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    // ===== Trends tab =====

    public async Task<Dictionary<DateTime, int>> GetReadingHeatmapAsync(int year, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Half-open [startOfYear, startOfNextYear): the inclusive last tick avoids the
        // 23:59:59.000-23:59:59.999 gap (GetSessionsInRangeAsync compares with <=).
        var endDate = startDate.AddYears(1).AddTicks(-1);

        var sessions = await unitOfWork.ReadingSessions.GetSessionsInRangeAsync(startDate, endDate, ct);

        return sessions
            .Where(s => s.Minutes > 0)
            .GroupBy(s => LocalTimeHelper.LocalDate(s.StartedAt, _timeZone))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public async Task<Dictionary<DayOfWeek, int>> GetWeekdayDistributionAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var sessions = await unitOfWork.ReadingSessions.GetAllAsync(ct);

        return sessions
            .Where(s => s.Minutes > 0)
            .GroupBy(s => LocalTimeHelper.ToLocal(s.StartedAt, _timeZone).DayOfWeek)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public async Task<Dictionary<string, int>> GetTimeOfDayDistributionAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var sessions = await unitOfWork.ReadingSessions.GetAllAsync(ct);

        var result = new Dictionary<string, int>
        {
            { "Morning", 0 },
            { "Afternoon", 0 },
            { "Evening", 0 },
            { "Night", 0 }
        };

        foreach (var session in sessions.Where(s => s.Minutes > 0))
        {
            int hour = LocalTimeHelper.ToLocal(session.StartedAt, _timeZone).Hour;
            string bucket = hour switch
            {
                >= 5 and <= 11 => "Morning",
                >= 12 and <= 16 => "Afternoon",
                >= 17 and <= 21 => "Evening",
                _ => "Night" // 22-4
            };
            result[bucket] += session.Minutes;
        }

        return result;
    }

    public async Task<Dictionary<string, int>> GetSessionLengthDistributionAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var sessions = await unitOfWork.ReadingSessions.GetAllAsync(ct);

        var result = new Dictionary<string, int>
        {
            { "<15", 0 },
            { "15-30", 0 },
            { "30-60", 0 },
            { "1-2h", 0 },
            { ">2h", 0 }
        };

        // INK-12: exclude 0-minute (pages-only) sessions so the length histogram uses the same
        // session population as every other distribution method (all filter Minutes > 0).
        foreach (var session in sessions.Where(s => s.Minutes > 0))
        {
            string bucket = session.Minutes switch
            {
                < 15 => "<15",
                < 30 => "15-30",
                < 60 => "30-60",
                < 120 => "1-2h",
                _ => ">2h"
            };
            result[bucket]++;
        }

        return result;
    }

    public async Task<Dictionary<int, int>> GetMonthlyVolumeAsync(int year, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var books = await unitOfWork.Books.GetAllAsync(ct);

        return books
            .Where(b => b.Status == ReadingStatus.Completed
                     && b.DateCompleted.HasValue
                     && b.DateCompleted.Value.Year == year)
            .GroupBy(b => b.DateCompleted!.Value.Month)
            .ToDictionary(
                g => g.Key,
                g => g.Count()
            );
    }

    public async Task<(double Current, double Previous)> GetReadingSpeedTrendAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var sessions = await unitOfWork.ReadingSessions.GetSessionsInRangeAsync(previousMonthStart, now, ct);

        var currentMonthSessions = sessions
            .Where(s => s.StartedAt >= currentMonthStart && s.Minutes > 0 && s.PagesRead.HasValue && s.PagesRead > 0)
            .ToList();

        var previousMonthSessions = sessions
            .Where(s => s.StartedAt >= previousMonthStart && s.StartedAt < currentMonthStart && s.Minutes > 0 && s.PagesRead.HasValue && s.PagesRead > 0)
            .ToList();

        double currentSpeed = CalculateSpeed(currentMonthSessions);
        double previousSpeed = CalculateSpeed(previousMonthSessions);

        return (Math.Round(currentSpeed, 0), Math.Round(previousSpeed, 0));
    }

    public async Task<(double CurrentAvg, double PreviousAvg)> GetAverageFinishTimeTrendAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sixtyDaysAgo = now.AddDays(-60);

        var books = await unitOfWork.Books.GetAllAsync(ct);

        var completedBooks = books
            .Where(b => b.Status == ReadingStatus.Completed
                     && b.DateStarted.HasValue
                     && b.DateCompleted.HasValue)
            .ToList();

        var currentPeriod = completedBooks
            .Where(b => b.DateCompleted!.Value >= thirtyDaysAgo && b.DateCompleted!.Value <= now)
            .ToList();

        var previousPeriod = completedBooks
            .Where(b => b.DateCompleted!.Value >= sixtyDaysAgo && b.DateCompleted!.Value < thirtyDaysAgo)
            .ToList();

        double currentAvg = currentPeriod.Count > 0
            ? currentPeriod.Average(b => (b.DateCompleted!.Value - b.DateStarted!.Value).TotalDays)
            : 0;

        double previousAvg = previousPeriod.Count > 0
            ? previousPeriod.Average(b => (b.DateCompleted!.Value - b.DateStarted!.Value).TotalDays)
            : 0;

        return (Math.Round(currentAvg, 1), Math.Round(previousAvg, 1));
    }

    // ===== Analysen tab =====

    public async Task<(YearStats Year1, YearStats Year2)> GetYearComparisonAsync(int year1, int year2, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        // Both years run sequentially on this one context — no concurrent operations.
        var stats1 = await BuildYearStatsAsync(unitOfWork, year1, ct);
        var stats2 = await BuildYearStatsAsync(unitOfWork, year2, ct);
        return (stats1, stats2);
    }

    public async Task<Dictionary<string, int>> GetGenreRadarDataAsync(int maxGenres = 8, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var books = await unitOfWork.Books.GetAllAsync(ct);
        var completedBookIds = books
            .Where(b => b.Status == ReadingStatus.Completed)
            .Select(b => b.Id)
            .ToHashSet();

        var bookGenres = await unitOfWork.BookGenres.GetAllAsync(ct);
        var genres = await unitOfWork.Genres.GetAllAsync(ct);
        var genreLookup = genres.ToDictionary(g => g.Id, g => g.Name);

        var result = bookGenres
            .Where(bg => completedBookIds.Contains(bg.BookId))
            .GroupBy(bg => genreLookup.TryGetValue(bg.GenreId, out var name) ? name : "Unknown")
            .GroupBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Count())
            )
            .OrderByDescending(kvp => kvp.Value)
            .Take(maxGenres)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return result;
    }

    public async Task<(int Completed, int Abandoned)> GetCompletionRateAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var books = await unitOfWork.Books.GetAllAsync(ct);

        var completed = books.Count(b => b.Status == ReadingStatus.Completed);
        var abandoned = books.Count(b => b.Status == ReadingStatus.Abandoned);

        return (completed, abandoned);
    }

    public async Task<Dictionary<string, int>> GetPageCountDistributionAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var books = await unitOfWork.Books.GetAllAsync(ct);

        var result = new Dictionary<string, int>
        {
            { "<200", 0 },
            { "200-400", 0 },
            { "400-600", 0 },
            { ">600", 0 }
        };

        foreach (var book in books.Where(b => b.Status == ReadingStatus.Completed && b.PageCount.HasValue))
        {
            string bucket = book.PageCount!.Value switch
            {
                < 200 => "<200",
                < 400 => "200-400",
                < 600 => "400-600",
                _ => ">600"
            };
            result[bucket]++;
        }

        return result;
    }

    public async Task<List<AuthorStats>> GetTopAuthorsAsync(int count = 5, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var unitOfWork = new UnitOfWork(context);

        var books = await unitOfWork.Books.GetAllAsync(ct);

        var result = books
            .Where(b => b.Status == ReadingStatus.Completed && !string.IsNullOrWhiteSpace(b.Author))
            .GroupBy(b => b.Author)
            .Select(g => new AuthorStats(
                g.Key,
                g.Count(),
                g.Sum(b => b.PageCount ?? 0)
            ))
            .OrderByDescending(a => a.BookCount)
            .ThenByDescending(a => a.TotalPages)
            .Take(count)
            .ToList();

        return result;
    }

    // ===== Private helpers =====

    private static async Task<YearStats> BuildYearStatsAsync(IUnitOfWork unitOfWork, int year, CancellationToken ct = default)
    {
        var books = await unitOfWork.Books.GetAllAsync(ct);
        var startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Half-open [startOfYear, startOfNextYear) via the inclusive last tick (see GetReadingHeatmapAsync).
        var endDate = startDate.AddYears(1).AddTicks(-1);

        var completedBooks = books
            .Where(b => b.Status == ReadingStatus.Completed && b.DateCompleted.HasValue && b.DateCompleted.Value.Year == year)
            .ToList();

        int booksCompleted = completedBooks.Count;
        int pagesRead = completedBooks.Sum(b => b.PageCount ?? 0);

        var sessions = await unitOfWork.ReadingSessions.GetSessionsInRangeAsync(startDate, endDate, ct);
        int minutesRead = sessions.Sum(s => s.Minutes);

        double averageRating = completedBooks
            .Where(b => b.AverageRating.HasValue)
            .Any()
            ? Math.Round(completedBooks
                .Where(b => b.AverageRating.HasValue)
                .Average(b => b.AverageRating!.Value), 1)
            : 0;

        return new YearStats(year, booksCompleted, pagesRead, minutesRead, averageRating);
    }

    private static double CalculateSpeed(List<ReadingSession> sessions)
    {
        if (sessions.Count == 0)
            return 0;

        int totalPages = sessions.Sum(s => s.PagesRead!.Value);
        int totalMinutes = sessions.Sum(s => s.Minutes);

        if (totalMinutes == 0)
            return 0;

        return (double)totalPages / ((double)totalMinutes / 60.0);
    }
}
