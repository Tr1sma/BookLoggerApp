using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service for managing user progression (XP, levels, coins).
/// </summary>
public class ProgressionService : IProgressionService
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IPlantService _plantService;

    public ProgressionService(
        IAppSettingsProvider settingsProvider,
        IPlantService plantService)
    {
        _settingsProvider = settingsProvider;
        _plantService = plantService;
    }

    public async Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId, bool hasStreak = false)
    {
        // 1. Calculate base XP from session (including streak bonus)
        var (minutesXp, pagesXp, longSessionXp, streakXp) = XpCalculator.CalculateSessionXpBreakdown(minutes, pagesRead, hasStreak);
        int baseXp = minutesXp + pagesXp + longSessionXp + streakXp;

        // 2. Get plant boost
        decimal plantBoost = await GetTotalPlantBoostAsync();

        // 3. Apply boost to get final XP
        int boostedXp = XpCalculator.ApplyPlantBoost(baseXp, plantBoost);
        int bonusXp = boostedXp - baseXp;

        // 4. Get current settings
        var settings = await _settingsProvider.GetSettingsAsync();
        int oldXp = settings.TotalXp;

        // 5. Add XP to user
        settings.TotalXp += boostedXp;
        settings.UpdatedAt = DateTime.UtcNow;

        // 6. Check for level-up and update UserLevel in the same settings instance
        var levelUpResult = await CheckAndProcessLevelUpAsync(oldXp, settings.TotalXp, settings);

        // 7. Save settings (with both TotalXp and UserLevel updated)
        await _settingsProvider.UpdateSettingsAsync(settings);

        // 8. Return result
        return new ProgressionResult
        {
            XpEarned = boostedXp,
            BaseXp = baseXp,
            MinutesXp = minutesXp,
            PagesXp = pagesXp,
            LongSessionBonusXp = longSessionXp,
            StreakBonusXp = streakXp,
            PlantBoostPercentage = plantBoost,
            BoostedXp = bonusXp,
            NewTotalXp = settings.TotalXp,
            LevelUp = levelUpResult
        };
    }

    public async Task<ProgressionResult> AwardBookCompletionXpAsync(Guid? activePlantId)
    {
        // 1. Calculate base XP for book completion
        int baseXp = XpCalculator.CalculateXpForBookCompletion();

        // 2. Get plant boost
        decimal plantBoost = await GetTotalPlantBoostAsync();

        // 3. Apply boost to get final XP
        int boostedXp = XpCalculator.ApplyPlantBoost(baseXp, plantBoost);
        int bonusXp = boostedXp - baseXp;

        // 4. Get current settings
        var settings = await _settingsProvider.GetSettingsAsync();
        int oldXp = settings.TotalXp;

        // 5. Add XP to user
        settings.TotalXp += boostedXp;
        settings.UpdatedAt = DateTime.UtcNow;

        // 6. Check for level-up and update UserLevel in the same settings instance
        var levelUpResult = await CheckAndProcessLevelUpAsync(oldXp, settings.TotalXp, settings);

        // 7. Save settings (with both TotalXp and UserLevel updated)
        await _settingsProvider.UpdateSettingsAsync(settings);

        // 8. Return result
        return new ProgressionResult
        {
            XpEarned = boostedXp,
            BaseXp = baseXp,
            BookCompletionXp = baseXp,
            PlantBoostPercentage = plantBoost,
            BoostedXp = bonusXp,
            NewTotalXp = settings.TotalXp,
            LevelUp = levelUpResult
        };
    }

    public async Task<decimal> GetTotalPlantBoostAsync()
    {
        // Get all user's plants (Species is eagerly loaded via Include)
        var userPlants = await _plantService.GetAllAsync();

        // Filter out dead plants — they should not provide XP boosts
        var alivePlants = userPlants.Where(p => p.Status != PlantStatus.Dead).ToList();

        if (!alivePlants.Any())
            return 0m;

        decimal totalBoost = 0m;

        foreach (var plant in alivePlants)
        {
            // Species is already loaded via Include in GetAllAsync — no extra DB query needed
            if (plant.Species == null)
                continue;

            // Calculate boost for this plant
            // Formula: baseBoost + (levelBonus per level)
            // Example: StarterSprout = 5% base + 0.5% per level
            // At level 5: 5% + (5 * 0.5%) = 7.5%
            decimal baseBoost = plant.Species.XpBoostPercentage;
            decimal levelBonus = plant.Species.MaxLevel > 0
                ? plant.CurrentLevel * (plant.Species.XpBoostPercentage / plant.Species.MaxLevel)
                : 0m;
            decimal plantBoost = baseBoost + levelBonus;

            totalBoost += plantBoost;
        }

        return totalBoost;
    }

    public async Task<LevelUpResult?> CheckAndProcessLevelUpAsync(int oldXp, int newXp, AppSettings? settingsToUpdate = null)
    {
        int oldLevel = XpCalculator.CalculateLevelFromXp(oldXp);
        int newLevel = XpCalculator.CalculateLevelFromXp(newXp);

        // No level-up occurred
        if (newLevel <= oldLevel)
            return null;

        // Calculate coins awarded (sum of all levels gained)
        // Formula: Level × 50 coins per level
        // Example: Level 3 → Level 5 = (4 × 50) + (5 × 50) = 200 + 250 = 450 coins
        int coinsAwarded = 0;
        for (int level = oldLevel + 1; level <= newLevel; level++)
        {
            coinsAwarded += level * 50;
        }

        int newCoins;

        // Update user level and coins in settings
        if (settingsToUpdate != null)
        {
            // Update the provided settings instance directly (caller will save)
            // This prevents the race condition where AddCoinsAsync would save to DB
            // but then UpdateSettingsAsync overwrites with old Coins value
            settingsToUpdate.UserLevel = newLevel;
            settingsToUpdate.Coins += coinsAwarded;
            settingsToUpdate.UpdatedAt = DateTime.UtcNow;
            newCoins = settingsToUpdate.Coins;
        }
        else
        {
            // Fetch, update, and save settings ourselves (backwards compatibility)
            // In this case we can use AddCoinsAsync since we control the full save
            await _settingsProvider.AddCoinsAsync(coinsAwarded);
            var settings = await _settingsProvider.GetSettingsAsync();
            settings.UserLevel = newLevel;
            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsProvider.UpdateSettingsAsync(settings);
            newCoins = settings.Coins;
        }

        return new LevelUpResult
        {
            OldLevel = oldLevel,
            NewLevel = newLevel,
            CoinsAwarded = coinsAwarded,
            NewTotalCoins = newCoins
        };
    }
}
