using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Mock implementation of IProgressionService for testing purposes.
/// Returns default/empty values for all operations.
/// </summary>
public class MockProgressionService : IProgressionService
{
    public Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId, bool hasStreak = false)
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
