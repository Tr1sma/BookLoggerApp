using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Manages user progression (XP, levels, coins).
/// </summary>
public class ProgressionService : IProgressionService
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IPlantService _plantService;
    private readonly IDecorationService _decorationService;
    private readonly IAnalyticsService _analytics;

    public ProgressionService(
        IAppSettingsProvider settingsProvider,
        IPlantService plantService,
        IDecorationService decorationService,
        IAnalyticsService? analytics = null)
    {
        _settingsProvider = settingsProvider;
        _plantService = plantService;
        _decorationService = decorationService;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
    }

    public async Task<ProgressionResult> AwardSessionXpAsync(int minutes, int? pagesRead, Guid? activePlantId, int streakDays = 0, CancellationToken ct = default)
    {
        var (minutesXp, pagesXp, longSessionXp, streakXp) = XpCalculator.CalculateSessionXpBreakdown(minutes, pagesRead, streakDays);
        int baseXp = minutesXp + pagesXp + longSessionXp + streakXp;

        decimal plantBoost = await GetTotalPlantBoostAsync(ct);

        int boostedXp = XpCalculator.ApplyPlantBoost(baseXp, plantBoost);
        int bonusXp = boostedXp - baseXp;

        var settings = await _settingsProvider.GetSettingsAsync(ct);
        int oldXp = settings.TotalXp;

        settings.TotalXp += boostedXp;
        settings.UpdatedAt = DateTime.UtcNow;

        // Updates UserLevel on the same settings instance so the single save below persists both.
        var levelUpResult = await CheckAndProcessLevelUpAsync(oldXp, settings.TotalXp, settings, ct);

        await _settingsProvider.UpdateSettingsAsync(settings, ct);

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

    public async Task<ProgressionResult> AwardBookCompletionXpAsync(Guid? activePlantId, CancellationToken ct = default)
    {
        int baseXp = XpCalculator.CalculateXpForBookCompletion();

        decimal plantBoost = await GetTotalPlantBoostAsync(ct);

        int boostedXp = XpCalculator.ApplyPlantBoost(baseXp, plantBoost);
        int bonusXp = boostedXp - baseXp;

        var settings = await _settingsProvider.GetSettingsAsync(ct);
        int oldXp = settings.TotalXp;

        settings.TotalXp += boostedXp;
        settings.UpdatedAt = DateTime.UtcNow;

        // Updates UserLevel on the same settings instance so the single save below persists both.
        var levelUpResult = await CheckAndProcessLevelUpAsync(oldXp, settings.TotalXp, settings, ct);

        await _settingsProvider.UpdateSettingsAsync(settings, ct);

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

    public async Task<decimal> GetTotalPlantBoostAsync(CancellationToken ct = default)
    {
        var userPlants = await _plantService.GetAllAsync(ct);
        bool hasStoryHeart = await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart, ct);
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

        var levelUpResult = await CheckAndProcessLevelUpAsync(oldXp, settings.TotalXp, settings, ct);
        await _settingsProvider.UpdateSettingsAsync(settings, ct);
        return levelUpResult;
    }

    public async Task<LevelUpResult?> CheckAndProcessLevelUpAsync(int oldXp, int newXp, AppSettings? settingsToUpdate = null, CancellationToken ct = default)
    {
        int oldLevel = XpCalculator.CalculateLevelFromXp(oldXp);
        int newLevel = XpCalculator.CalculateLevelFromXp(newXp);

        if (newLevel <= oldLevel)
            return null;

        // Coins awarded = sum over each level gained (50 × Level + 3 × Level², progressive).
        int coinsAwarded = 0;
        for (int level = oldLevel + 1; level <= newLevel; level++)
        {
            coinsAwarded += XpCalculator.CalculateCoinsForLevel(level);
        }

        if (await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart, ct))
        {
            coinsAwarded = (int)Math.Round(coinsAwarded * SpecialAbilityResolver.StoryHeartCoinMultiplier);
        }

        int newCoins;

        if (settingsToUpdate != null)
        {
            // Mutate the caller's settings instance (caller saves). Avoids the race where a separate
            // AddCoinsAsync save is then overwritten by UpdateSettingsAsync with the old Coins value.
            settingsToUpdate.UserLevel = newLevel;
            settingsToUpdate.Coins += coinsAwarded;
            settingsToUpdate.UpdatedAt = DateTime.UtcNow;
            newCoins = settingsToUpdate.Coins;
        }
        else
        {
            // Fetch, apply coins + level, save once. Two separate saves previously risked losing
            // coin updates in a race.
            _settingsProvider.InvalidateCache();
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            settings.Coins += coinsAwarded;
            settings.UserLevel = newLevel;
            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsProvider.UpdateSettingsAsync(settings, ct);
            newCoins = settings.Coins;
        }

        var result = new LevelUpResult
        {
            OldLevel = oldLevel,
            NewLevel = newLevel,
            CoinsAwarded = coinsAwarded,
            NewTotalCoins = newCoins
        };

        _analytics.LogEvent(AnalyticsEventNames.LevelUp, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.NewLevelBucket, AnalyticsBuckets.Level(newLevel))
            .BuildMutable());

        return result;
    }
}
