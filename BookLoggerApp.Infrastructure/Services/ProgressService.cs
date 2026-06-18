using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

public class ProgressService : IProgressService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressionService _progressionService;
    private readonly IPlantService _plantService;
    private readonly IBookService _bookService;
    private readonly IGoalService _goalService;
    private readonly IDecorationService _decorationService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IAnalyticsService _analytics;

    public ProgressService(
        IUnitOfWork unitOfWork,
        IProgressionService progressionService,
        IPlantService plantService,
        IBookService bookService,
        IGoalService goalService,
        IDecorationService decorationService,
        IAppSettingsProvider settingsProvider,
        IAnalyticsService? analytics = null)
    {
        _unitOfWork = unitOfWork;
        _progressionService = progressionService;
        _plantService = plantService;
        _bookService = bookService;
        _goalService = goalService;
        _decorationService = decorationService;
        _settingsProvider = settingsProvider;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
    }

    public async Task<SessionSaveResult> AddSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        var activePlant = await _plantService.GetActivePlantAsync(ct);

        // Snapshot level BEFORE XP award: Story-Heart first-of-day bonus must use pre-session level
        // even if the session itself triggers a level-up.
        var settingsBeforeSession = await _settingsProvider.GetSettingsAsync(ct);
        int levelAtSessionStart = settingsBeforeSession.UserLevel;

        // Award streak bonus only on first qualifying session of the day
        var streak = await ResolveStreakForSessionAsync(session, ct);

        var progressionResult = await _progressionService.AwardSessionXpAsync(
            session.Minutes,
            session.PagesRead,
            activePlant?.Id,
            streak.StreakDays,
            ct
        );

        session.XpEarned = progressionResult.XpEarned;

        var result = await _unitOfWork.ReadingSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync(ct);

        if (streak.GuardianToUpdate.HasValue)
        {
            await PersistGuardianCooldownAsync(streak.GuardianToUpdate.Value, ct);
        }

        // Plants level via reading days (15+ min), not XP
        if (activePlant != null)
        {
            await _plantService.RecordReadingDayAsync(
                activePlant.Id,
                session.StartedAt,
                session.Minutes,
                ct
            );
        }

        var heartBonus = await ApplyStoryHeartSessionBonusesAsync(session, levelAtSessionStart, ct);

        bool goalCompleted = await _goalService.RecalculateGoalProgressAsync(ct);

        _goalService.NotifyGoalsChanged();

        return new SessionSaveResult
        {
            Session = result,
            ProgressionResult = progressionResult,
            GoalCompleted = goalCompleted,
            StreakRescuedByGuardian = streak.RescuedByGuardian,
            StoryHeartCoinBonus = heartBonus.CoinBonus,
            StoryHeartFirstOfDayBonusXp = heartBonus.BonusXp,
            StoryHeartLevelUp = heartBonus.LevelUp
        };
    }

    public async Task<ReadingSession> StartSessionAsync(Guid bookId, CancellationToken ct = default)
    {
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

        _analytics.LogEvent(AnalyticsEventNames.SessionStarted, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.FromReadingPage, true)
            .BuildMutable());

        return result;
    }

    public async Task<SessionEndResult> EndSessionAsync(Guid sessionId, int pagesRead, int? durationMinutes = null, CancellationToken ct = default)
    {
        if (pagesRead < 0)
            throw new ArgumentOutOfRangeException(nameof(pagesRead), "Pages read cannot be negative");

        var session = await _unitOfWork.ReadingSessions.GetByIdAsync(sessionId);
        if (session == null)
            throw new EntityNotFoundException(typeof(ReadingSession), sessionId);

        var startPage = session.StartPage ?? 0;
        var absoluteEndPage = startPage + pagesRead;

        var book = await _bookService.GetByIdAsync(session.BookId, ct);
        if (book?.PageCount.HasValue == true && absoluteEndPage > book.PageCount.Value)
            throw new ArgumentOutOfRangeException(nameof(pagesRead),
                $"Session end page ({absoluteEndPage}) exceeds book page count ({book.PageCount.Value}). Start page: {startPage}, pages read: {pagesRead}.");

        session.EndedAt = DateTime.UtcNow;
        session.PagesRead = pagesRead;
        if (durationMinutes.HasValue)
        {
            session.Minutes = Math.Clamp(durationMinutes.Value, 0, 1440);
            session.StartedAt = session.EndedAt.Value.AddMinutes(-session.Minutes);
        }
        else
        {
            session.Minutes = Math.Max(0, (int)(session.EndedAt.Value - session.StartedAt).TotalMinutes);
        }

        var activePlant = await _plantService.GetActivePlantAsync(ct);

        // Snapshot level BEFORE XP award (see AddSessionAsync)
        var settingsBeforeSession = await _settingsProvider.GetSettingsAsync(ct);
        int levelAtSessionStart = settingsBeforeSession.UserLevel;

        // Award streak bonus only on first qualifying session of the day
        var streak = await ResolveStreakForSessionAsync(session, ct);

        var progressionResult = await _progressionService.AwardSessionXpAsync(
            session.Minutes,
            pagesRead,
            activePlant?.Id,
            streak.StreakDays,
            ct
        );

        session.XpEarned = progressionResult.XpEarned;

        // Plants level via reading days (15+ min), not XP
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

        if (streak.GuardianToUpdate.HasValue)
        {
            await PersistGuardianCooldownAsync(streak.GuardianToUpdate.Value, ct);
        }

        var heartBonus = await ApplyStoryHeartSessionBonusesAsync(session, levelAtSessionStart, ct);

        bool goalCompleted = await _goalService.RecalculateGoalProgressAsync(ct);

        _goalService.NotifyGoalsChanged();

        _analytics.LogEvent(AnalyticsEventNames.SessionEnded, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.MinutesBucket, AnalyticsBuckets.Minutes(session.Minutes))
            .Add(AnalyticsParamNames.PagesReadBucket, AnalyticsBuckets.Pages(pagesRead))
            .Add(AnalyticsParamNames.XpEarnedBucket, AnalyticsBuckets.XpDelta(progressionResult.XpEarned))
            .Add(AnalyticsParamNames.TriggeredLevelUp, progressionResult.LevelUp is not null)
            .BuildMutable());

        return new SessionEndResult
        {
            Session = session,
            ProgressionResult = progressionResult,
            GoalCompleted = goalCompleted,
            StreakRescuedByGuardian = streak.RescuedByGuardian,
            StoryHeartCoinBonus = heartBonus.CoinBonus,
            StoryHeartFirstOfDayBonusXp = heartBonus.BonusXp,
            StoryHeartLevelUp = heartBonus.LevelUp
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

        // Cap at 1 year: streaks longer than 365 days are unrealistic, avoids
        // loading thousands of records for long-time users.
        var recentSessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(today.AddDays(-365), DateTime.UtcNow);

        return ReadingStreakHelper.CalculateCurrentStreak(recentSessions, today);
    }

    private readonly record struct ResolvedStreak(int StreakDays, bool RescuedByGuardian, Guid? GuardianToUpdate);

    private readonly record struct StoryHeartBonus(int CoinBonus, int BonusXp, LevelUpResult? LevelUp);

    private async Task<ResolvedStreak> ResolveStreakForSessionAsync(ReadingSession session, CancellationToken ct)
    {
        if (!ReadingStreakHelper.CountsTowardStreak(session))
        {
            return new ResolvedStreak(0, false, null);
        }

        var sessionDate = session.StartedAt.Date;
        var rangeEnd = sessionDate.AddDays(1).AddTicks(-1);

        var recentSessions = (await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(sessionDate.AddDays(-365), rangeEnd)).ToList();

        var alreadyAwardedToday = recentSessions.Any(existingSession =>
            existingSession.Id != session.Id
            && existingSession.StartedAt.Date == sessionDate
            && ReadingStreakHelper.CountsTowardStreak(existingSession));

        if (alreadyAwardedToday)
        {
            return new ResolvedStreak(0, false, null);
        }

        var priorSessions = recentSessions.Where(s => s.Id != session.Id).ToList();
        var regularStreak = ReadingStreakHelper.CalculateInclusiveStreak(priorSessions, sessionDate);

        // Streak-Wächter: fires only when regular streak would be 1 (yesterday missed),
        // a prior streak day existed day-before-yesterday, and an alive Chronikbaum is off cooldown.
        if (regularStreak != 1)
        {
            return new ResolvedStreak(regularStreak, false, null);
        }

        var dayBeforeYesterday = sessionDate.AddDays(-2);
        bool priorStreakExists = priorSessions
            .Where(ReadingStreakHelper.CountsTowardStreak)
            .Any(s => s.StartedAt.Date == dayBeforeYesterday);

        if (!priorStreakExists)
        {
            return new ResolvedStreak(regularStreak, false, null);
        }

        var allPlants = await _plantService.GetAllAsync(ct);
        var utcNow = DateTime.UtcNow;
        var guardian = allPlants.FirstOrDefault(p =>
            p.Species?.SpecialAbilityKey == SpecialAbilityKeys.StreakGuardian
            && p.Status != PlantStatus.Dead
            && SpecialAbilityResolver.CanGuardianSaveStreak(p, utcNow));

        if (guardian is null)
        {
            return new ResolvedStreak(regularStreak, false, null);
        }

        var rescuedDates = priorSessions
            .Where(ReadingStreakHelper.CountsTowardStreak)
            .Select(s => s.StartedAt.Date)
            .ToList();
        rescuedDates.Add(sessionDate.AddDays(-1));
        var rescuedStreak = ReadingStreakHelper.CalculateInclusiveStreak(rescuedDates, sessionDate);

        // Defer guardian cooldown persistence until after session save to avoid burning it on failure
        return new ResolvedStreak(rescuedStreak, true, guardian.Id);
    }

    private async Task PersistGuardianCooldownAsync(Guid guardianId, CancellationToken ct)
    {
        var guardianEntity = await _unitOfWork.UserPlants.GetByIdAsync(guardianId);
        if (guardianEntity is not null)
        {
            guardianEntity.LastStreakSaveAt = DateTime.UtcNow;
            await _unitOfWork.UserPlants.UpdateAsync(guardianEntity);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task<StoryHeartBonus> ApplyStoryHeartSessionBonusesAsync(ReadingSession session, int levelAtSessionStart, CancellationToken ct)
    {
        if (!await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart, ct))
        {
            return new StoryHeartBonus(0, 0, null);
        }

        int coinBonus = 0;
        int bonusXp = 0;
        LevelUpResult? bonusLevelUp = null;

        if (session.Minutes >= SpecialAbilityResolver.StoryHeartSessionMinMinutes)
        {
            await _settingsProvider.AddCoinsAsync(SpecialAbilityResolver.StoryHeartSessionCoinBonus, ct);
            coinBonus = SpecialAbilityResolver.StoryHeartSessionCoinBonus;
        }

        var sessionDate = session.StartedAt.Date;
        var dayRangeEnd = sessionDate.AddDays(1).AddTicks(-1);
        var sessionsToday = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(sessionDate, dayRangeEnd);

        bool isFirstQualifyingSession = ReadingStreakHelper.CountsTowardStreak(session)
            && !sessionsToday.Any(existing =>
                existing.Id != session.Id
                && ReadingStreakHelper.CountsTowardStreak(existing));

        if (isFirstQualifyingSession)
        {
            int xpForNextLevel = XpCalculator.GetXpForLevel(levelAtSessionStart);
            bonusXp = (int)Math.Round(xpForNextLevel * SpecialAbilityResolver.StoryHeartFirstSessionXpPct);

            if (bonusXp > 0)
            {
                // Capture LevelUp here: bonus XP is awarded after main session save,
                // so any level-up it triggers won't appear in ProgressionResult.LevelUp.
                bonusLevelUp = await _progressionService.AwardBonusXpAsync(bonusXp, ct);
            }
        }

        return new StoryHeartBonus(coinBonus, bonusXp, bonusLevelUp);
    }
}
