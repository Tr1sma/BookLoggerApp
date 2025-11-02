using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for calculating reading statistics.
/// </summary>
public class StatsService : IStatsService
{
    private readonly IBookRepository _bookRepository;
    private readonly IReadingSessionRepository _sessionRepository;
    private readonly AppDbContext _context;

    public StatsService(
        IBookRepository bookRepository,
        IReadingSessionRepository sessionRepository,
        AppDbContext context)
    {
        _bookRepository = bookRepository;
        _sessionRepository = sessionRepository;
        _context = context;
    }

    public async Task<int> GetTotalBooksReadAsync(CancellationToken ct = default)
    {
        return await _bookRepository.CountAsync(b => b.Status == ReadingStatus.Completed);
    }

    public async Task<int> GetTotalPagesReadAsync(CancellationToken ct = default)
    {
        var completedBooks = await _bookRepository.GetBooksByStatusAsync(ReadingStatus.Completed);
        return completedBooks.Where(b => b.PageCount.HasValue).Sum(b => b.PageCount!.Value);
    }

    public async Task<int> GetTotalMinutesReadAsync(CancellationToken ct = default)
    {
        var allSessions = await _sessionRepository.GetAllAsync();
        return allSessions.Sum(s => s.Minutes);
    }

    public async Task<int> GetCurrentStreakAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var allSessions = await _sessionRepository.GetAllAsync();

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
        var allSessions = await _sessionRepository.GetAllAsync();
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
        var sessions = await _sessionRepository.GetSessionsInRangeAsync(start, end);

        return sessions
            .GroupBy(s => s.StartedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public async Task<int> GetPagesReadInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _sessionRepository.GetSessionsInRangeAsync(start, end);
        return sessions.Where(s => s.PagesRead.HasValue).Sum(s => s.PagesRead!.Value);
    }

    public async Task<int> GetBooksCompletedInYearAsync(int year, CancellationToken ct = default)
    {
        var books = await _bookRepository.GetBooksByStatusAsync(ReadingStatus.Completed);
        return books.Count(b => b.DateCompleted.HasValue && b.DateCompleted.Value.Year == year);
    }

    public async Task<Dictionary<string, int>> GetBooksByGenreAsync(CancellationToken ct = default)
    {
        var books = await _context.Books
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
        var books = await _bookRepository.GetAllAsync();
        var ratedBooks = books.Where(b => b.Rating.HasValue).ToList();

        if (!ratedBooks.Any())
            return 0;

        return ratedBooks.Average(b => b.Rating!.Value);
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

        var sessions = await _sessionRepository.GetSessionsInRangeAsync(start, end);
        var totalMinutes = sessions.Sum(s => s.Minutes);

        return (double)totalMinutes / days;
    }
}
