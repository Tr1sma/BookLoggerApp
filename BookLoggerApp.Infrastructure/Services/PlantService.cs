using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services.Helpers;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Manages user plants with caching support.
///
/// <para><b>Concurrency model (BUG-10 / INK-07):</b> only <c>AppSettings</c> and
/// <c>UserEntitlement</c> carry an app-stamped RowVersion token. <c>UserPlant</c> is
/// deliberately last-writer-wins (RowVersion is a non-token), so the
/// <see cref="DbUpdateConcurrencyException"/> handlers below do NOT detect write conflicts —
/// they translate EF's missing-row signal (concurrent delete) into a friendly
/// <see cref="ConcurrencyException"/>.</para>
/// </summary>
public class PlantService : IPlantService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IDecorationService _decorationService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlantService> _logger;
    private readonly IAnalyticsService _analytics;
    private readonly IFeatureGuard? _featureGuard;
    private readonly IValidationService? _validation;
    private const string SpeciesCacheKey = "AllPlantSpecies";

    public PlantService(
        IUnitOfWork unitOfWork,
        IAppSettingsProvider settingsProvider,
        IDecorationService decorationService,
        IMemoryCache cache,
        ILogger<PlantService> logger,
        IAnalyticsService? analytics = null,
        IFeatureGuard? featureGuard = null,
        IValidationService? validation = null)
    {
        _unitOfWork = unitOfWork;
        _settingsProvider = settingsProvider;
        _decorationService = decorationService;
        _cache = cache;
        _logger = logger;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
        _featureGuard = featureGuard;
        _validation = validation;
    }

    public async Task<IReadOnlyList<UserPlant>> GetAllAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync(ct);
        await ApplyDisplayStatusesAsync(plants, ct);
        return plants.ToList();
    }

    public async Task<UserPlant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(id, ct);
        if (plant != null)
        {
            await ApplyDisplayStatusAsync(plant, ct);
        }

        return plant;
    }

    public async Task<UserPlant> AddAsync(UserPlant plant, CancellationToken ct = default)
    {
        if (plant.PlantedAt == default)
            plant.PlantedAt = DateTime.UtcNow;

        if (plant.LastWatered == default)
            plant.LastWatered = DateTime.UtcNow;

        // BUG-05: validate after timestamp defaults are filled (validator requires PlantedAt/LastWatered).
        if (_validation is not null)
            await _validation.ValidateAndThrowAsync(plant, ct);

        var result = await _unitOfWork.UserPlants.AddAsync(plant, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task UpdateAsync(UserPlant plant, CancellationToken ct = default)
    {
        // BUG-05: validate the edited plant before persisting, like the create path.
        if (_validation is not null)
            await _validation.ValidateAndThrowAsync(plant, ct);

        try
        {
            await _unitOfWork.UserPlants.UpdateAsync(plant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict updating plant {PlantId}", plant.Id);
            throw new ConcurrencyException($"Plant with ID {plant.Id} was modified by another user. Please reload and try again.", ex);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetByIdAsync(id, ct);
        if (plant != null)
        {
            var plantShelves = await _unitOfWork.Context.PlantShelves
                .Where(ps => ps.PlantId == id)
                .ToListAsync(ct);

            if (plantShelves.Count > 0)
            {
                _unitOfWork.Context.PlantShelves.RemoveRange(plantShelves);
            }

            await _unitOfWork.UserPlants.DeleteAsync(plant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetActivePlantAsync(ct);
        if (plant != null)
        {
            await ApplyDisplayStatusAsync(plant, ct);
        }

        return plant;
    }

    public async Task SetActivePlantAsync(Guid plantId, CancellationToken ct = default)
    {
        var exists = await _unitOfWork.UserPlants.ExistsAsync(p => p.Id == plantId, ct);
        if (!exists)
            throw new EntityNotFoundException(typeof(UserPlant), plantId);

        // Load-Update-Save instead of ExecuteUpdateAsync for InMemory provider compatibility in tests.
        var allPlants = await _unitOfWork.UserPlants.GetAllAsync(ct);

        foreach (var plant in allPlants)
        {
            plant.IsActive = plant.Id == plantId;
            await _unitOfWork.UserPlants.UpdateAsync(plant, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PlantSpecies>> GetAllSpeciesAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(SpeciesCacheKey, out List<PlantSpecies>? cached))
            return cached!;

        var species = await _unitOfWork.PlantSpecies.GetAllAsync(ct);
        var list = species.ToList();

        // Cache for 24 hours (plant species rarely change).
        _cache.Set(SpeciesCacheKey, list, TimeSpan.FromHours(24));
        return list;
    }

    public async Task<PlantSpecies?> GetSpeciesByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.PlantSpecies.GetByIdAsync(id, ct);
    }

    public async Task WaterPlantAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId, ct);
        if (plant == null)
            throw new EntityNotFoundException(typeof(UserPlant), plantId);

        await RefreshPlantStatusAsync(plant, ct);

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot water a dead plant");

        plant.LastWatered = DateTime.UtcNow;

        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        plant.Status = PlantGrowthCalculator.CalculatePlantStatus(plant.LastWatered, plant.Species.WaterIntervalDays, growthMultiplier);

        try
        {
            await _unitOfWork.UserPlants.UpdateAsync(plant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict watering plant {PlantId}", plantId);
            throw new ConcurrencyException($"Plant with ID {plantId} was modified by another user. Please reload and try again.", ex);
        }
    }

    // Z.692: AddExperienceAsync (XP-based plant leveling) removed — dead path, see IPlantService note.

    /// <summary>
    /// Records a reading day (session >= 15 min, once per plant per day) and updates the plant's level. 3 reading days = 1 level at GrowthRate 1.0.
    /// </summary>
    public async Task RecordReadingDayAsync(Guid plantId, DateTime sessionDate, int sessionMinutes, CancellationToken ct = default)
    {
        // Minimum 15 minutes required for a reading day.
        if (sessionMinutes < 15)
            return;

        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId, ct);
        if (plant == null)
            return;

        await RefreshPlantStatusAsync(plant, ct);

        // Dead plants don't earn reading days.
        if (plant.Status == PlantStatus.Dead)
            return;

        var sessionDay = sessionDate.Date;

        // Skip if a reading day was already recorded today for this plant.
        if (plant.LastReadingDayRecorded?.Date == sessionDay)
            return;

        plant.ReadingDaysCount++;
        plant.LastReadingDayRecorded = sessionDay;

        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        int newLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(
            plant.ReadingDaysCount,
            plant.Species.GrowthRate,
            plant.Species.MaxLevel,
            growthMultiplier
        );

        // Compute coin payout but defer persistence until the plant save succeeds,
        // otherwise a failed plant save would leave the user with the coins but no level-up.
        int coinsAwarded = 0;
        if (newLevel > plant.CurrentLevel)
        {
            int levelsGained = newLevel - plant.CurrentLevel;
            plant.CurrentLevel = newLevel;
            coinsAwarded = levelsGained * 100;
        }

        try
        {
            await _unitOfWork.UserPlants.UpdateAsync(plant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict recording reading day for plant {PlantId}", plantId);
            throw new ConcurrencyException($"Plant with ID {plantId} was modified by another user. Please reload and try again.", ex);
        }

        if (coinsAwarded > 0)
        {
            await _settingsProvider.AddCoinsAsync(coinsAwarded, ct);
            _logger.LogInformation("Plant {PlantId} leveled up to {Level} after {ReadingDays} reading days, awarded {Coins} coins",
                plantId, newLevel, plant.ReadingDaysCount, coinsAwarded);
        }
    }

    // Z.692: CanLevelUpAsync / LevelUpAsync (XP-based plant leveling) removed — dead path, see
    // IPlantService note. Coin-purchased leveling lives in PurchaseLevelAsync below.

    public async Task PurchaseLevelAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId, ct);
        if (plant == null)
            throw new EntityNotFoundException(typeof(UserPlant), plantId);

        await RefreshPlantStatusAsync(plant, ct);

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot level up a dead plant");

        if (plant.CurrentLevel >= plant.Species.MaxLevel)
            throw new InvalidOperationException("Plant is already at max level");

        // 100 coins per level.
        int cost = (plant.CurrentLevel + 1) * 100;

        // Throws if not enough coins.
        await _settingsProvider.SpendCoinsAsync(cost, ct);

        // Separate DbContexts can't share one EF transaction; refund coins explicitly if the plant save fails.
        try
        {
            plant.CurrentLevel++;
            await _unitOfWork.UserPlants.UpdateAsync(plant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plant level-up save failed after coins were spent. Refunding {Cost} coins for plant {PlantId}", cost, plantId);
            try
            {
                await _settingsProvider.AddCoinsAsync(cost, ct);
            }
            catch (Exception refundEx)
            {
                _logger.LogCritical(refundEx, "CRITICAL: Could not refund {Cost} coins after failed level-up for plant {PlantId}. User coin balance is incorrect.", cost, plantId);
            }
            throw;
        }
    }

    public async Task<UserPlant> PurchasePlantAsync(Guid speciesId, string name, CancellationToken ct = default)
    {
        var species = await _unitOfWork.PlantSpecies.GetByIdAsync(speciesId, ct);
        if (species == null)
            throw new EntityNotFoundException(typeof(PlantSpecies), speciesId);

        if (!species.IsAvailable)
            throw new InvalidOperationException("Plant species is not available for purchase");

        // Entitlement gate (SEC-08): UI lock is a hint only — enforce the plant's tier here so no
        // caller can buy a Plus/Premium plant without the subscription.
        if (_featureGuard is not null)
        {
            FeatureKey? tierFeature = ShopTierFeatures.For(species);
            if (tierFeature is not null)
            {
                _featureGuard.RequireAccess(tierFeature.Value, "This plant requires a higher subscription tier.");
            }
        }

        int cost = await GetPlantCostAsync(speciesId, ct);

        var plant = new UserPlant
        {
            SpeciesId = speciesId,
            Name = name,
            CurrentLevel = 1,
            Experience = 0,
            PlantedAt = DateTime.UtcNow,
            LastWatered = DateTime.UtcNow,
            IsActive = false
        };

        // BUG-05: validate the new plant BEFORE charging coins, so an invalid name can't deduct coins then fail the save.
        if (_validation is not null)
            await _validation.ValidateAndThrowAsync(plant, ct);

        // Throws if not enough coins.
        await _settingsProvider.SpendCoinsAsync(cost, ct);

        // Separate DbContexts can't share one EF transaction; refund coins explicitly if the plant save fails.
        UserPlant result;
        try
        {
            result = await _unitOfWork.UserPlants.AddAsync(plant, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plant purchase save failed after coins were spent. Refunding {Cost} coins for species {SpeciesId}", cost, speciesId);
            try
            {
                await _settingsProvider.AddCoinsAsync(cost, ct);
            }
            catch (Exception refundEx)
            {
                _logger.LogCritical(refundEx, "CRITICAL: Could not refund {Cost} coins after failed plant purchase for species {SpeciesId}. User coin balance is incorrect.", cost, speciesId);
            }
            throw;
        }

        // Increment PlantsPurchased only after the purchase succeeded, so a failed save doesn't raise
        // the dynamic price. A failure here is self-correcting (next plant priced as if unbought).
        try
        {
            await _settingsProvider.IncrementPlantsPurchasedAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plant purchase succeeded but PlantsPurchased counter increment failed for species {SpeciesId}. Dynamic pricing for the next plant may be slightly off.", speciesId);
        }

        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            _analytics.LogEvent(AnalyticsEventNames.PlantPurchased, AnalyticsParamBuilder.Create()
                .Add(AnalyticsParamNames.SpeciesKey, SanitizeKey(species.Name))
                .Add(AnalyticsParamNames.LevelAtPurchaseBucket, AnalyticsBuckets.Level(settings.UserLevel))
                .Add(AnalyticsParamNames.TotalPlantsBucket, AnalyticsBuckets.Plants(settings.PlantsPurchased))
                .Add(AnalyticsParamNames.PriceBucket, AnalyticsBuckets.Coins(cost))
                .BuildMutable());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PlantPurchased event logging failed: {ex}");
        }

        return result;
    }

    private static string SanitizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var slug = new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        return slug.Length > 32 ? slug.Substring(0, 32) : slug;
    }

    /// <summary>
    /// Updates all plant statuses based on last watered time. Called periodically (app start, background).
    /// </summary>
    public async Task UpdatePlantStatusesAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync(ct);
        await RefreshPlantStatusesAsync(plants, ct);
    }

    /// <summary>
    /// Gets plants needing watering soon (within 6 hours). Honours the global growth multiplier so notifications match the UI thirst timeline.
    /// </summary>
    public async Task<IReadOnlyList<UserPlant>> GetPlantsNeedingWaterAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync(ct);
        await ApplyDisplayStatusesAsync(plants, ct);

        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        return plants
            .Where(p => p.Status != PlantStatus.Dead)
            .Where(p => PlantGrowthCalculator.NeedsWateringSoon(p.LastWatered, p.Species.WaterIntervalDays, growthMultiplier))
            .ToList();
    }

    /// <summary>
    /// Gets species available for purchase at the given user level.
    /// </summary>
    public async Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default)
    {
        var species = await _unitOfWork.PlantSpecies.FindAsync(s => s.IsAvailable && s.UnlockLevel <= userLevel, ct);
        return species.OrderBy(s => s.BaseCost).ToList();
    }

    /// <summary>
    /// Total XP boost percentage from all owned plants. Delegates to <see cref="SpecialAbilityResolver.CalculateAggregatedPlantBoost"/> so UI and XP-award agree.
    /// </summary>
    public async Task<decimal> CalculateTotalXpBoostAsync(CancellationToken ct = default)
    {
        var allPlants = await _unitOfWork.UserPlants.GetUserPlantsAsync(ct);
        await ApplyDisplayStatusesAsync(allPlants, ct);
        bool hasStoryHeart = await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart, ct);
        return SpecialAbilityResolver.CalculateAggregatedPlantBoost(allPlants, hasStoryHeart);
    }

    /// <summary>
    /// Dynamic purchase cost for a species: BaseCost + (PlantsPurchased × 200).
    /// </summary>
    public async Task<int> GetPlantCostAsync(Guid speciesId, CancellationToken ct = default)
    {
        var species = await _unitOfWork.PlantSpecies.GetByIdAsync(speciesId, ct);
        if (species == null)
            throw new EntityNotFoundException(typeof(PlantSpecies), speciesId);

        int plantsPurchased = await _settingsProvider.GetPlantsPurchasedAsync(ct);

        int dynamicCost = species.BaseCost + (plantsPurchased * 200);

        return dynamicCost;
    }

    /// <summary>
    /// Z.206 — read-only status projection for getter paths: recomputes <see cref="UserPlant.Status"/>
    /// in memory only. Does NOT persist and does NOT reset a Phoenix's water timer (that lives in
    /// <see cref="UpdatePlantStatusesAsync"/> and the mutators), so a plain read never writes.
    /// </summary>
    private async Task ApplyDisplayStatusesAsync(IEnumerable<UserPlant> plants, CancellationToken ct)
    {
        var plantList = plants as IList<UserPlant> ?? plants.ToList();
        bool userOwnsPhoenix = SpecialAbilityResolver.AnyAlivePlantHasAbility(plantList, SpecialAbilityKeys.EternalPhoenix);
        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        foreach (var plant in plantList)
        {
            ApplyDisplayStatus(plant, userOwnsPhoenix, growthMultiplier);
        }
    }

    private async Task ApplyDisplayStatusAsync(UserPlant plant, CancellationToken ct)
    {
        // Phoenix protection spans all plants, so load the full set even for a single projection.
        var allPlants = await _unitOfWork.UserPlants.GetUserPlantsAsync(ct);
        bool userOwnsPhoenix = SpecialAbilityResolver.AnyAlivePlantHasAbility(allPlants, SpecialAbilityKeys.EternalPhoenix);
        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        ApplyDisplayStatus(plant, userOwnsPhoenix, growthMultiplier);
    }

    private static void ApplyDisplayStatus(UserPlant plant, bool userOwnsPhoenix, double growthMultiplier)
    {
        if (plant.Species == null)
            return;

        var currentStatus = PlantGrowthCalculator.CalculatePlantStatus(
            plant.LastWatered,
            plant.Species.WaterIntervalDays,
            growthMultiplier);

        // Phoenix protection (display only): never SHOW Dead while a Phoenix is owned. Unlike the
        // persist path, a read must not reset the Phoenix's LastWatered (Z.206).
        if (currentStatus == PlantStatus.Dead && userOwnsPhoenix)
        {
            currentStatus = PlantStatus.Wilting;
        }

        plant.Status = currentStatus;
    }

    private async Task RefreshPlantStatusesAsync(IEnumerable<UserPlant> plants, CancellationToken ct)
    {
        var plantList = plants as IList<UserPlant> ?? plants.ToList();
        bool userOwnsPhoenix = SpecialAbilityResolver.AnyAlivePlantHasAbility(plantList, SpecialAbilityKeys.EternalPhoenix);
        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        bool hasChanges = false;

        foreach (var plant in plantList)
        {
            if (!TryApplyCurrentPlantStatus(plant, userOwnsPhoenix, growthMultiplier))
                continue;

            hasChanges = true;
            await _unitOfWork.UserPlants.UpdateAsync(plant, ct);
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task RefreshPlantStatusAsync(UserPlant plant, CancellationToken ct)
    {
        // Phoenix protection spans all plants, so load the full set even for a single refresh.
        var allPlants = await _unitOfWork.UserPlants.GetUserPlantsAsync(ct);
        bool userOwnsPhoenix = SpecialAbilityResolver.AnyAlivePlantHasAbility(allPlants, SpecialAbilityKeys.EternalPhoenix);
        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        if (!TryApplyCurrentPlantStatus(plant, userOwnsPhoenix, growthMultiplier))
            return;

        await _unitOfWork.UserPlants.UpdateAsync(plant, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<double> GetGlobalGrowthMultiplierAsync(CancellationToken ct)
    {
        return await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart, ct)
            ? (double)SpecialAbilityResolver.StoryHeartPlantGrowthMultiplier
            : 1.0;
    }

    private static bool TryApplyCurrentPlantStatus(UserPlant plant, bool userOwnsPhoenix, double growthMultiplier)
    {
        if (plant.Species == null)
            return false;

        var currentStatus = PlantGrowthCalculator.CalculatePlantStatus(
            plant.LastWatered,
            plant.Species.WaterIntervalDays,
            growthMultiplier
        );

        // Phoenix protection: while a Phoenix is owned, no plant can transition to Dead (also
        // handles the Phoenix's self-revival). Reset the Phoenix's LastWatered so it doesn't
        // re-enter this branch every refresh.
        bool mutatedLastWatered = false;
        if (currentStatus == PlantStatus.Dead && userOwnsPhoenix)
        {
            currentStatus = PlantStatus.Wilting;

            if (plant.Species.SpecialAbilityKey == SpecialAbilityKeys.EternalPhoenix)
            {
                plant.LastWatered = DateTime.UtcNow;
                mutatedLastWatered = true;
            }
        }

        if (plant.Status == currentStatus)
        {
            return mutatedLastWatered;
        }

        plant.Status = currentStatus;
        return true;
    }
}
