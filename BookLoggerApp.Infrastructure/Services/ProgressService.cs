using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Infrastructure.Services.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for tracking reading progress.
/// </summary>
public class ProgressService : IProgressService
{
    private readonly IReadingSessionRepository _sessionRepository;

    public ProgressService(IReadingSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        // Calculate XP
        var hasStreak = await HasReadingStreakAsync(ct);
        session.XpEarned = XpCalculator.CalculateXpForSession(session.Minutes, session.PagesRead, hasStreak);

        return await _sessionRepository.AddAsync(session);
    }

    public async Task<ReadingSession> StartSessionAsync(Guid bookId, CancellationToken ct = default)
    {
        var session = new ReadingSession
        {
            BookId = bookId,
            StartedAt = DateTime.UtcNow
        };

        return await _sessionRepository.AddAsync(session);
    }

    public async Task<ReadingSession> EndSessionAsync(Guid sessionId, int pagesRead, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
            throw new ArgumentException("Session not found", nameof(sessionId));

        session.EndedAt = DateTime.UtcNow;
        session.PagesRead = pagesRead;
        session.Minutes = (int)(session.EndedAt.Value - session.StartedAt).TotalMinutes;

        // Calculate XP
        var hasStreak = await HasReadingStreakAsync(ct);
        session.XpEarned = XpCalculator.CalculateXpForSession(session.Minutes, pagesRead, hasStreak);

        await _sessionRepository.UpdateAsync(session);
        return session;
    }

    public async Task UpdateSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        await _sessionRepository.UpdateAsync(session);
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session != null)
        {
            await _sessionRepository.DeleteAsync(session);
        }
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var sessions = await _sessionRepository.GetSessionsByBookAsync(bookId);
        return sessions.ToList();
    }

    public async Task<IReadOnlyList<ReadingSession>> GetRecentSessionsAsync(int count = 10, CancellationToken ct = default)
    {
        var sessions = await _sessionRepository.GetRecentSessionsAsync(count);
        return sessions.ToList();
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _sessionRepository.GetSessionsInRangeAsync(start, end);
        return sessions.ToList();
    }

    public async Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _sessionRepository.GetTotalMinutesReadAsync(bookId);
    }

    public async Task<int> GetTotalPagesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _sessionRepository.GetTotalPagesReadAsync(bookId);
    }

    public async Task<int> GetTotalMinutesAllBooksAsync(CancellationToken ct = default)
    {
        var allSessions = await _sessionRepository.GetAllAsync();
        return allSessions.Sum(s => s.Minutes);
    }

    public async Task<Dictionary<DateTime, int>> GetMinutesByDateAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _sessionRepository.GetSessionsInRangeAsync(start, end);

        return sessions
            .GroupBy(s => s.StartedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
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

        // Check if user read today or yesterday
        var mostRecentDate = sessionsByDate.First().Key;
        if ((today - mostRecentDate).Days > 1)
            return 0; // Streak broken

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

    private async Task<bool> HasReadingStreakAsync(CancellationToken ct = default)
    {
        var streak = await GetCurrentStreakAsync(ct);
        return streak >= 2; // At least 2 days in a row
    }
}
