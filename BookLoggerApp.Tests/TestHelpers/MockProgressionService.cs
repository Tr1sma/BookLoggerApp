using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Mock implementation of IProgressionService for testing purposes.
/// Calculates XP using XpCalculator (without plant boost or settings persistence).
/// </summary>
public class MockProgressionService : IProgressionService
{
    /// <summary>Number of times <see cref="AwardBookCompletionXpAsync"/> has been called.</summary>
    public int AwardBookCompletionXpCallCount { get; private set; }

    public Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId, int streakDays = 0, CancellationToken ct = default)
    {
        var (minutesXp, pagesXp, longSessionXp, streakXp) = XpCalculator.CalculateSessionXpBreakdown(minutes, pagesRead, streakDays);
        int baseXp = minutesXp + pagesXp + longSessionXp + streakXp;

        return Task.FromResult(new ProgressionResult
        {
            XpEarned = baseXp,
            BaseXp = baseXp,
            MinutesXp = minutesXp,
            PagesXp = pagesXp,
            LongSessionBonusXp = longSessionXp,
            StreakBonusXp = streakXp,
            StreakDays = streakDays,
            BoostedXp = 0,
            PlantBoostPercentage = 0,
            NewTotalXp = baseXp,
            LevelUp = null
        });
    }

    public Task<ProgressionResult> AwardBookCompletionXpAsync(Guid? activePlantId, CancellationToken ct = default)
    {
        AwardBookCompletionXpCallCount++;
        return Task.FromResult(new ProgressionResult
        {
            XpEarned = 0,
            BaseXp = 0,
            BoostedXp = 0,
            PlantBoostPercentage = 0,
            LevelUp = null
        });
    }

    public Task<decimal> GetTotalPlantBoostAsync(CancellationToken ct = default)
    {
        return Task.FromResult(0m);
    }

    public Task<LevelUpResult?> CheckAndProcessLevelUpAsync(int oldXp, int newXp, AppSettings? settingsToUpdate = null, CancellationToken ct = default)
    {
        return Task.FromResult<LevelUpResult?>(null);
    }

    public Task<LevelUpResult?> AwardBonusXpAsync(int xp, CancellationToken ct = default)
    {
        return Task.FromResult<LevelUpResult?>(null);
    }
}
