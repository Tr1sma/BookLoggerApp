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
    public Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId, bool hasStreak = false)
    {
        var (minutesXp, pagesXp, longSessionXp, streakXp) = XpCalculator.CalculateSessionXpBreakdown(minutes, pagesRead, hasStreak);
        int baseXp = minutesXp + pagesXp + longSessionXp + streakXp;

        return Task.FromResult(new ProgressionResult
        {
            XpEarned = baseXp,
            BaseXp = baseXp,
            MinutesXp = minutesXp,
            PagesXp = pagesXp,
            LongSessionBonusXp = longSessionXp,
            StreakBonusXp = streakXp,
            BoostedXp = 0,
            PlantBoostPercentage = 0,
            NewTotalXp = baseXp,
            LevelUp = null
        });
    }

    public Task<ProgressionResult> AwardBookCompletionXpAsync(Guid? activePlantId)
    {
        return Task.FromResult(new ProgressionResult
        {
            XpEarned = 0,
            BaseXp = 0,
            BoostedXp = 0,
            PlantBoostPercentage = 0,
            LevelUp = null
        });
    }

    public Task<decimal> GetTotalPlantBoostAsync()
    {
        return Task.FromResult(0m);
    }

    public Task<LevelUpResult?> CheckAndProcessLevelUpAsync(int oldXp, int newXp, AppSettings? settingsToUpdate = null)
    {
        return Task.FromResult<LevelUpResult?>(null);
    }
}
