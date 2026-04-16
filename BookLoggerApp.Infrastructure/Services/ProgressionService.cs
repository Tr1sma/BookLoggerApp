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
    private readonly IDecorationService _decorationService;

    public ProgressionService(
        IAppSettingsProvider settingsProvider,
        IPlantService plantService,
        IDecorationService decorationService)
    {
        _settingsProvider = settingsProvider;
        _plantService = plantService;
        _decorationService = decorationService;
    }

    public async Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId, int streakDays = 0)
    {
        // 1. Calculate base XP from session (including streak bonus)
        var (minutesXp, pagesXp, longSessionXp, streakXp) = XpCalculator.CalculateSessionXpBreakdown(minutes, pagesRead, streakDays);
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
            StreakDays = streakDays,
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
        var userPlants = await _plantService.GetAllAsync();
        bool hasStoryHeart = await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart);
        return SpecialAbilityResolver.CalculateAggregatedPlantBoost(userPlants, hasStoryHeart);
    }

    public async Task<LevelUpResult?> AwardBonusXpAsync(int xp, CancellationToken ct = default)
    {
        if (xp <= 0)
            return null;

        var settings = await _settingsProvider.GetSettingsAsync(ct);
        int oldXp = settings.TotalXp;
        settings.TotalXp += xp;
        settings.UpdatedAt = DateTime.UtcNow;

        var levelUpResult = await CheckAndProcessLevelUpAsync(oldXp, settings.TotalXp, settings);
        await _settingsProvider.UpdateSettingsAsync(settings, ct);
        return levelUpResult;
    }

    public async Task<LevelUpResult?> CheckAndProcessLevelUpAsync(int oldXp, int newXp, AppSettings? settingsToUpdate = null)
    {
        int oldLevel = XpCalculator.CalculateLevelFromXp(oldXp);
        int newLevel = XpCalculator.CalculateLevelFromXp(newXp);

        // No level-up occurred
        if (newLevel <= oldLevel)
            return null;

        // Calculate coins awarded (sum of all levels gained)
        // Formula: 50 × Level + 3 × Level² (progressive scaling)
        // Example: Level 3 → Level 5 = CalculateCoinsForLevel(4) + CalculateCoinsForLevel(5) = 248 + 325 = 573 coins
        int coinsAwarded = 0;
        for (int level = oldLevel + 1; level <= newLevel; level++)
        {
            coinsAwarded += XpCalculator.CalculateCoinsForLevel(level);
        }

        if (await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart))
        {
            coinsAwarded = (int)Math.Round(coinsAwarded * SpecialAbilityResolver.StoryHeartCoinMultiplier);
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
            // Fetch settings, apply both coins and level atomically, then save once.
            // Previously this called AddCoinsAsync (separate save) then UpdateSettingsAsync
            // (another save), which could lose coin updates in a race condition.
            _settingsProvider.InvalidateCache();
            var settings = await _settingsProvider.GetSettingsAsync();
            settings.Coins += coinsAwarded;
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
