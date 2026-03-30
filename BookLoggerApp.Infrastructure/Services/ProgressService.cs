using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for tracking reading progress.
/// </summary>
public class ProgressService : IProgressService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressionService _progressionService;
    private readonly IPlantService _plantService;
    private readonly IBookService _bookService;
    private readonly IGoalService _goalService;

    public ProgressService(
        IUnitOfWork unitOfWork,
        IProgressionService progressionService,
        IPlantService plantService,
        IBookService bookService,
        IGoalService goalService)
    {
        _unitOfWork = unitOfWork;
        _progressionService = progressionService;
        _plantService = plantService;
        _bookService = bookService;
        _goalService = goalService;
    }

    public async Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        // Get active plant for boost calculation
        var activePlant = await _plantService.GetActivePlantAsync(ct);

        // Check for reading streak
        var hasStreak = await HasReadingStreakAsync(ct);

        // Award XP using the progression system (with plant boost and streak bonus)
        var progressionResult = await _progressionService.AwardSessionXpAsync(
            session.Minutes,
            session.PagesRead,
            activePlant?.Id,
            hasStreak
        );

        session.XpEarned = progressionResult.XpEarned;

        var result = await _unitOfWork.ReadingSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync(ct);

        // Record reading day for active plant (for plant leveling)
        // Plants level up based on reading days (15+ min sessions), not XP
        if (activePlant != null)
        {
            await _plantService.RecordReadingDayAsync(
                activePlant.Id,
                session.StartedAt,
                session.Minutes,
                ct
            );
        }

        // Notify that goals may have changed
        _goalService.NotifyGoalsChanged();

        return result;
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

        var result = await _unitOfWork.ReadingSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task<SessionEndResult> EndSessionAsync(Guid sessionId, int pagesRead, CancellationToken ct = default)
    {
        // Validate input
        if (pagesRead < 0)
            throw new ArgumentOutOfRangeException(nameof(pagesRead), "Pages read cannot be negative");

        var session = await _unitOfWork.ReadingSessions.GetByIdAsync(sessionId);
        if (session == null)
            throw new EntityNotFoundException(typeof(ReadingSession), sessionId);

        // Validate pages read against book page count
        var book = await _bookService.GetByIdAsync(session.BookId, ct);
        if (book?.PageCount.HasValue == true && pagesRead > book.PageCount.Value)
            throw new ArgumentOutOfRangeException(nameof(pagesRead),
                $"Pages read ({pagesRead}) exceeds book page count ({book.PageCount.Value})");

        session.EndedAt = DateTime.UtcNow;
        session.PagesRead = pagesRead;
        session.Minutes = Math.Max(0, (int)(session.EndedAt.Value - session.StartedAt).TotalMinutes);

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

        // Record reading day for active plant (for plant leveling)
        // Plants level up based on reading days (15+ min sessions), not XP
        if (activePlant != null)
        {
            await _plantService.RecordReadingDayAsync(
                activePlant.Id,
                session.StartedAt,
                session.Minutes,
                ct
            );
        }

        await _unitOfWork.ReadingSessions.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync(ct);

        // Notify that goals may have changed (pages/minutes progress)
        _goalService.NotifyGoalsChanged();

        // Return both session and progression result for UI celebrations
        return new SessionEndResult
        {
            Session = session,
            ProgressionResult = progressionResult
        };
    }

    public async Task UpdateSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        await _unitOfWork.ReadingSessions.UpdateAsync(session);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.ReadingSessions.GetByIdAsync(sessionId);
        if (session != null)
        {
            await _unitOfWork.ReadingSessions.DeleteAsync(session);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<ReadingSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetByIdAsync(sessionId, ct);
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsByBookAsync(bookId);
        return sessions.ToList();
    }

    public async Task<IReadOnlyList<ReadingSession>> GetRecentSessionsAsync(int count = 10, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetRecentSessionsAsync(count);
        return sessions.ToList();
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end);
        return sessions.ToList();
    }

    public async Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetTotalMinutesReadAsync(bookId);
    }

    public async Task<int> GetTotalPagesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetTotalPagesReadAsync(bookId);
    }

    public async Task<int> GetTotalMinutesAllBooksAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetTotalMinutesAsync(ct);
    }

    public async Task<Dictionary<DateTime, int>> GetMinutesByDateAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end);

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

        // Only load sessions from the last year instead of ALL sessions.
        // A streak longer than 365 days is unrealistic, and this avoids
        // loading thousands of records for long-time users.
        var recentSessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(today.AddDays(-365), DateTime.UtcNow);

        var sessionsByDate = recentSessions
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
