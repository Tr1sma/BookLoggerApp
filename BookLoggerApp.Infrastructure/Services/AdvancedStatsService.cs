using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for advanced reading statistics (trends and analyses).
/// </summary>
public class AdvancedStatsService : IAdvancedStatsService
{
    private readonly IUnitOfWork _unitOfWork;

    public AdvancedStatsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // ===== Trends tab =====

    public async Task<Dictionary<DateTime, int>> GetReadingHeatmapAsync(int year, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(startDate, endDate);

        return sessions
            .Where(s => s.Minutes > 0)
            .GroupBy(s => s.StartedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public async Task<Dictionary<DayOfWeek, int>> GetWeekdayDistributionAsync(CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetAllAsync(ct);

        return sessions
            .Where(s => s.Minutes > 0)
            .GroupBy(s => s.StartedAt.DayOfWeek)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public async Task<Dictionary<string, int>> GetTimeOfDayDistributionAsync(CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetAllAsync(ct);

        var result = new Dictionary<string, int>
        {
            { "Morning", 0 },
            { "Afternoon", 0 },
            { "Evening", 0 },
            { "Night", 0 }
        };

        foreach (var session in sessions.Where(s => s.Minutes > 0))
        {
            int hour = session.StartedAt.Hour;
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
        var sessions = await _unitOfWork.ReadingSessions.GetAllAsync(ct);

        var result = new Dictionary<string, int>
        {
            { "<15", 0 },
            { "15-30", 0 },
            { "30-60", 0 },
            { "1-2h", 0 },
            { ">2h", 0 }
        };

        foreach (var session in sessions)
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
        var books = await _unitOfWork.Books.GetAllAsync(ct);

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
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(previousMonthStart, now);

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
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sixtyDaysAgo = now.AddDays(-60);

        var books = await _unitOfWork.Books.GetAllAsync(ct);

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

    // ===== Analysen tab (stubs) =====

    public Task<(YearStats Year1, YearStats Year2)> GetYearComparisonAsync(int year1, int year2, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, int>> GetGenreRadarDataAsync(int maxGenres = 8, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<(int Completed, int Abandoned)> GetCompletionRateAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, int>> GetPageCountDistributionAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<List<AuthorStats>> GetTopAuthorsAsync(int count = 5, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    // ===== Private helpers =====

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
