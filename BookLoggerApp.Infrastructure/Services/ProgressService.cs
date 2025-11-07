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
    private readonly IProgressionService _progressionService;
    private readonly IPlantService _plantService;
    private readonly IBookService _bookService;

    public ProgressService(
        IReadingSessionRepository sessionRepository,
        IProgressionService progressionService,
        IPlantService plantService,
        IBookService bookService)
    {
        _sessionRepository = sessionRepository;
        _progressionService = progressionService;
        _plantService = plantService;
        _bookService = bookService;
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
        // Start reading the book if it's in Planned status
        var book = await _bookService.GetByIdAsync(bookId, ct);
        if (book?.Status == ReadingStatus.Planned)
        {
            await _bookService.StartReadingAsync(bookId, ct);
        }

        var session = new ReadingSession
        {
            BookId = bookId,
            StartedAt = DateTime.UtcNow
        };

        return await _sessionRepository.AddAsync(session);
    }

    public async Task<SessionEndResult> EndSessionAsync(Guid sessionId, int pagesRead, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null)
            throw new ArgumentException("Session not found", nameof(sessionId));

        session.EndedAt = DateTime.UtcNow;
        session.PagesRead = pagesRead;
        session.Minutes = (int)(session.EndedAt.Value - session.StartedAt).TotalMinutes;

        // Get active plant for boost calculation
        var activePlant = await _plantService.GetActivePlantAsync(ct);

        // Check for reading streak
        var hasStreak = await HasReadingStreakAsync(ct);

        // Award XP using the new progression system (with streak bonus)
        var progressionResult = await _progressionService.AwardSessionXpAsync(
            session.Minutes,
            pagesRead,
            activePlant?.Id,
            hasStreak
        );

        // Store the XP earned in the session
        session.XpEarned = progressionResult.XpEarned;

        // Award XP to active plant if exists
        if (activePlant != null)
        {
            // Award plant XP (typically a fraction of user XP, or based on minutes)
            int plantXp = session.Minutes * 2; // 2 XP per minute for plants
            await _plantService.AddExperienceAsync(activePlant.Id, plantXp, ct);
        }

        await _sessionRepository.UpdateAsync(session);

        // Return both session and progression result for UI celebrations
        return new SessionEndResult
        {
            Session = session,
            ProgressionResult = progressionResult
        };
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
