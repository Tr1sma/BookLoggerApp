using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Tracks reading progress.
/// </summary>
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
    private readonly IValidationService? _validation;
    private readonly TimeZoneInfo _timeZone;

    public ProgressService(
        IUnitOfWork unitOfWork,
        IProgressionService progressionService,
        IPlantService plantService,
        IBookService bookService,
        IGoalService goalService,
        IDecorationService decorationService,
        IAppSettingsProvider settingsProvider,
        IAnalyticsService? analytics = null,
        IValidationService? validation = null,
        TimeZoneInfo? timeZone = null)
    {
        _unitOfWork = unitOfWork;
        _progressionService = progressionService;
        _plantService = plantService;
        _bookService = bookService;
        _goalService = goalService;
        _decorationService = decorationService;
        _settingsProvider = settingsProvider;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
        _validation = validation;
        // LOG-02: streak days use the local calendar; injectable (default Local) for deterministic tests.
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public async Task<SessionSaveResult> AddSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        // BUG-05: validate the externally supplied session before awarding XP or persisting.
        if (_validation is not null)
            await _validation.ValidateAndThrowAsync(session, ct);

        var activePlant = await _plantService.GetActivePlantAsync(ct);

        // Snapshot level BEFORE session XP: the Story-Heart first-of-day bonus uses the pre-session level.
        var settingsBeforeSession = await _settingsProvider.GetSettingsAsync(ct);
        int levelAtSessionStart = settingsBeforeSession.UserLevel;

        // Streak bonus only on the first qualifying session of the day.
        var streak = await ResolveStreakForSessionAsync(session, ct);

        var progressionResult = await _progressionService.AwardSessionXpAsync(
            session.Minutes,
            session.PagesRead,
            activePlant?.Id,
            streak.StreakDays,
            ct
        );

        session.XpEarned = progressionResult.XpEarned;

        var result = await _unitOfWork.ReadingSessions.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        if (streak.GuardianToUpdate.HasValue)
        {
            await PersistGuardianCooldownAsync(streak.GuardianToUpdate.Value, ct);
        }

        // Plants level up on reading days (15+ min sessions), not XP.
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
        // Start reading the book if it's still Planned.
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

        var result = await _unitOfWork.ReadingSessions.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _analytics.LogEvent(AnalyticsEventNames.SessionStarted, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.FromReadingPage, true)
            .BuildMutable());

        return result;
    }

    public async Task<SessionEndResult> EndSessionAsync(Guid sessionId, int pagesRead, int? durationMinutes = null, IReadOnlyList<SessionMood>? moods = null, CancellationToken ct = default)
    {
        if (pagesRead < 0)
            throw new ArgumentOutOfRangeException(nameof(pagesRead), "Pages read cannot be negative");

        var session = await _unitOfWork.ReadingSessions.GetByIdAsync(sessionId, ct);
        if (session == null)
            throw new EntityNotFoundException(typeof(ReadingSession), sessionId);

        var startPage = session.StartPage ?? 0;
        var absoluteEndPage = startPage + pagesRead;

        // Validate end page against book page count.
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

        // Z.375 — Session completion fans out writes across DISTINCT transient DbContexts (each
        // service owns its own connection), so no single EF transaction can span the chain.
        // Deliberately non-atomic and ordered so expensive-to-repeat side effects (guardian
        // cooldown, Story-Heart bonuses) run only AFTER the session row commits — a failed save
        // never burns a cooldown or double-awards. Each step is idempotent-safe on retry; in this
        // single-user offline app a mid-chain crash leaves at worst a stale total the next session fixes.

        var activePlant = await _plantService.GetActivePlantAsync(ct);

        // Snapshot level BEFORE session XP (see AddSessionAsync).
        var settingsBeforeSession = await _settingsProvider.GetSettingsAsync(ct);
        int levelAtSessionStart = settingsBeforeSession.UserLevel;

        // Streak bonus only on the first qualifying session of the day.
        var streak = await ResolveStreakForSessionAsync(session, ct);

        var progressionResult = await _progressionService.AwardSessionXpAsync(
            session.Minutes,
            pagesRead,
            activePlant?.Id,
            streak.StreakDays,
            ct
        );

        session.XpEarned = progressionResult.XpEarned;

        // Persist mood/trigger tags (1-3); navigation is empty so just add child rows.
        foreach (var mood in SessionMoodHelper.Clamp(moods))
        {
            session.Moods.Add(new ReadingSessionMood { ReadingSessionId = session.Id, Mood = mood });
        }

        // Plants level up on reading days (15+ min sessions), not XP.
        if (activePlant != null)
        {
            await _plantService.RecordReadingDayAsync(
                activePlant.Id,
                session.StartedAt,
                session.Minutes,
                ct
            );
        }

        await _unitOfWork.ReadingSessions.UpdateAsync(session, ct);
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
        // BUG-05: validate the edited session before persisting, like AddSessionAsync.
        if (_validation is not null)
            await _validation.ValidateAndThrowAsync(session, ct);

        await _unitOfWork.ReadingSessions.UpdateAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _unitOfWork.ReadingSessions.GetByIdAsync(sessionId, ct);
        if (session != null)
        {
            await _unitOfWork.ReadingSessions.DeleteAsync(session, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<ReadingSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetByIdAsync(sessionId, ct);
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsByBookAsync(bookId, ct);
        return sessions.ToList();
    }

    public async Task<IReadOnlyList<ReadingSession>> GetRecentSessionsAsync(int count = 10, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetRecentSessionsAsync(count, ct);
        return sessions.ToList();
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end, ct);
        return sessions.ToList();
    }

    public async Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetTotalMinutesReadAsync(bookId, ct);
    }

    public async Task<int> GetTotalPagesAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetTotalPagesReadAsync(bookId, ct);
    }

    public async Task<int> GetTotalMinutesAllBooksAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingSessions.GetTotalMinutesAsync(ct);
    }

    public async Task<Dictionary<DateTime, int>> GetMinutesByDateAsync(DateTime start, DateTime end, CancellationToken ct = default)
    {
        var sessions = await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(start, end, ct);

        return sessions
            .GroupBy(s => s.StartedAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.Minutes)
            );
    }

    public Task<int> GetCurrentStreakAsync(CancellationToken ct = default)
        => GetCurrentStreakAsync(DateTime.UtcNow, ct);

    // Internal clock seam so local-day streak logic stays deterministically testable with a fixed "now".
    internal async Task<int> GetCurrentStreakAsync(DateTime utcNow, CancellationToken ct)
    {
        // LOG-02: anchor "today" to the local calendar to match the goals feature's local-midnight convention.
        var localToday = LocalTimeHelper.LocalDate(utcNow, _timeZone);

        // Load only the last year (a >365-day streak is unrealistic; avoids loading thousands of rows).
        var recentSessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(utcNow.AddDays(-365), utcNow, ct);

        return ReadingStreakHelper.CalculateCurrentStreak(recentSessions, localToday, _timeZone);
    }

    private readonly record struct ResolvedStreak(int StreakDays, bool RescuedByGuardian, Guid? GuardianToUpdate);

    private readonly record struct StoryHeartBonus(int CoinBonus, int BonusXp, LevelUpResult? LevelUp);

    private async Task<ResolvedStreak> ResolveStreakForSessionAsync(ReadingSession session, CancellationToken ct)
    {
        if (!ReadingStreakHelper.CountsTowardStreak(session))
        {
            return new ResolvedStreak(0, false, null);
        }

        // LOG-02: bucket sessions by LOCAL calendar day so UTC-midnight crossings attribute consistently.
        // The range query still filters by the raw UTC instant.
        var sessionDate = LocalTimeHelper.LocalDate(session.StartedAt, _timeZone);

        var recentSessions = (await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(session.StartedAt.AddDays(-365), session.StartedAt.AddDays(1), ct)).ToList();

        var alreadyAwardedToday = recentSessions.Any(existingSession =>
            existingSession.Id != session.Id
            && LocalTimeHelper.LocalDate(existingSession.StartedAt, _timeZone) == sessionDate
            && ReadingStreakHelper.CountsTowardStreak(existingSession));

        if (alreadyAwardedToday)
        {
            return new ResolvedStreak(0, false, null);
        }

        var priorSessions = recentSessions.Where(s => s.Id != session.Id).ToList();
        var regularStreak = ReadingStreakHelper.CalculateInclusiveStreak(priorSessions, sessionDate, _timeZone);

        // Streak guardian: fires only when the regular streak would be 1 (yesterday missed),
        // a qualifying session existed day-before-yesterday (prior streak), and an alive
        // guardian plant is off cooldown.
        if (regularStreak != 1)
        {
            return new ResolvedStreak(regularStreak, false, null);
        }

        var dayBeforeYesterday = sessionDate.AddDays(-2);
        bool priorStreakExists = priorSessions
            .Where(ReadingStreakHelper.CountsTowardStreak)
            .Any(s => LocalTimeHelper.LocalDate(s.StartedAt, _timeZone) == dayBeforeYesterday);

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

        // Fill the missing yesterday and recompute.
        var rescuedDates = priorSessions
            .Where(ReadingStreakHelper.CountsTowardStreak)
            .Select(s => LocalTimeHelper.LocalDate(s.StartedAt, _timeZone))
            .ToList();
        rescuedDates.Add(sessionDate.AddDays(-1));
        var rescuedStreak = ReadingStreakHelper.CalculateInclusiveStreak(rescuedDates, sessionDate);

        // Defer the guardian cooldown persistence to after the session save so that
        // a failed session save does not burn the cooldown.
        return new ResolvedStreak(rescuedStreak, true, guardian.Id);
    }

    private async Task PersistGuardianCooldownAsync(Guid guardianId, CancellationToken ct)
    {
        var guardianEntity = await _unitOfWork.UserPlants.GetByIdAsync(guardianId, ct);
        if (guardianEntity is not null)
        {
            guardianEntity.LastStreakSaveAt = DateTime.UtcNow;
            await _unitOfWork.UserPlants.UpdateAsync(guardianEntity, ct);
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

        // LOG-02: "first qualifying session of the day" uses the LOCAL calendar day. Load a wide UTC
        // window (a local day can straddle two UTC days) and match by local day in memory.
        var sessionLocalDate = LocalTimeHelper.LocalDate(session.StartedAt, _timeZone);
        var nearbySessions = await _unitOfWork.ReadingSessions
            .GetSessionsInRangeAsync(session.StartedAt.AddDays(-1), session.StartedAt.AddDays(1), ct);

        bool isFirstQualifyingSession = ReadingStreakHelper.CountsTowardStreak(session)
            && !nearbySessions.Any(existing =>
                existing.Id != session.Id
                && LocalTimeHelper.LocalDate(existing.StartedAt, _timeZone) == sessionLocalDate
                && ReadingStreakHelper.CountsTowardStreak(existing));

        if (isFirstQualifyingSession)
        {
            int xpForNextLevel = XpCalculator.GetXpForLevel(levelAtSessionStart);
            bonusXp = (int)Math.Round(xpForNextLevel * SpecialAbilityResolver.StoryHeartFirstSessionXpPct);

            if (bonusXp > 0)
            {
                // Capture the LevelUp for the caller's celebration: bonus XP is awarded AFTER the
                // main session save, so its level-up isn't in ProgressionResult.LevelUp.
                bonusLevelUp = await _progressionService.AwardBonusXpAsync(bonusXp, ct);
            }
        }

        return new StoryHeartBonus(coinBonus, bonusXp, bonusLevelUp);
    }
}
