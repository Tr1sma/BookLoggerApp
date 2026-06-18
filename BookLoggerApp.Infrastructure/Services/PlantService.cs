using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services.Helpers;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Infrastructure.Services;

public class PlantService : IPlantService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IDecorationService _decorationService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlantService> _logger;
    private readonly IAnalyticsService _analytics;
    private const string SpeciesCacheKey = "AllPlantSpecies";

    public PlantService(
        IUnitOfWork unitOfWork,
        IAppSettingsProvider settingsProvider,
        IDecorationService decorationService,
        IMemoryCache cache,
        ILogger<PlantService> logger,
        IAnalyticsService? analytics = null)
    {
        _unitOfWork = unitOfWork;
        _settingsProvider = settingsProvider;
        _decorationService = decorationService;
        _cache = cache;
        _logger = logger;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
    }

    public async Task<IReadOnlyList<UserPlant>> GetAllAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        await RefreshPlantStatusesAsync(plants, ct);
        return plants.ToList();
    }

    public async Task<UserPlant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(id);
        if (plant != null)
        {
            await RefreshPlantStatusAsync(plant, ct);
        }

        return plant;
    }

    public async Task<UserPlant> AddAsync(UserPlant plant, CancellationToken ct = default)
    {
        if (plant.PlantedAt == default)
            plant.PlantedAt = DateTime.UtcNow;

        if (plant.LastWatered == default)
            plant.LastWatered = DateTime.UtcNow;

        var result = await _unitOfWork.UserPlants.AddAsync(plant);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task UpdateAsync(UserPlant plant, CancellationToken ct = default)
    {
        try
        {
            await _unitOfWork.UserPlants.UpdateAsync(plant);
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
        var plant = await _unitOfWork.UserPlants.GetByIdAsync(id);
        if (plant != null)
        {
            var plantShelves = await _unitOfWork.Context.PlantShelves
                .Where(ps => ps.PlantId == id)
                .ToListAsync(ct);

            if (plantShelves.Count > 0)
            {
                _unitOfWork.Context.PlantShelves.RemoveRange(plantShelves);
            }

            await _unitOfWork.UserPlants.DeleteAsync(plant);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetActivePlantAsync();
        if (plant != null)
        {
            await RefreshPlantStatusAsync(plant, ct);
        }

        return plant;
    }

    public async Task SetActivePlantAsync(Guid plantId, CancellationToken ct = default)
    {
        var exists = await _unitOfWork.UserPlants.ExistsAsync(p => p.Id == plantId, ct);
        if (!exists)
            throw new EntityNotFoundException(typeof(UserPlant), plantId);

        // Load-Update-Save: ExecuteUpdateAsync incompatible with InMemory test provider
        var allPlants = await _unitOfWork.UserPlants.GetAllAsync();

        foreach (var plant in allPlants)
        {
            plant.IsActive = plant.Id == plantId;
            await _unitOfWork.UserPlants.UpdateAsync(plant);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PlantSpecies>> GetAllSpeciesAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(SpeciesCacheKey, out List<PlantSpecies>? cached))
            return cached!;

        var species = await _unitOfWork.PlantSpecies.GetAllAsync(ct);
        var list = species.ToList();

        // 24h: species rarely change
        _cache.Set(SpeciesCacheKey, list, TimeSpan.FromHours(24));
        return list;
    }

    public async Task<PlantSpecies?> GetSpeciesByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.PlantSpecies.GetByIdAsync(id);
    }

    public async Task WaterPlantAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId);
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
            await _unitOfWork.UserPlants.UpdateAsync(plant);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict watering plant {PlantId}", plantId);
            throw new ConcurrencyException($"Plant with ID {plantId} was modified by another user. Please reload and try again.", ex);
        }
    }

    public async Task AddExperienceAsync(Guid plantId, int xp, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new EntityNotFoundException(typeof(UserPlant), plantId);

        await RefreshPlantStatusAsync(plant, ct);

        // Dead plants don't earn XP
        if (plant.Status == PlantStatus.Dead)
            return;

        plant.Experience += xp;

        int newLevel = PlantGrowthCalculator.CalculateLevelFromXp(
            plant.Experience,
            plant.Species.GrowthRate,
            plant.Species.MaxLevel
        );

        // Defer coin persistence until plant save succeeds to avoid charging for a failed level-up
        int coinsAwarded = 0;
        if (newLevel > plant.CurrentLevel)
        {
            int levelsGained = newLevel - plant.CurrentLevel;
            plant.CurrentLevel = newLevel;
            coinsAwarded = levelsGained * 100;
        }

        try
        {
            await _unitOfWork.UserPlants.UpdateAsync(plant);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict adding experience to plant {PlantId}", plantId);
            throw new ConcurrencyException($"Plant with ID {plantId} was modified by another user. Please reload and try again.", ex);
        }

        if (coinsAwarded > 0)
        {
            await _settingsProvider.AddCoinsAsync(coinsAwarded, ct);
            _logger.LogInformation("Plant {PlantId} leveled up to {Level}, awarded {Coins} coins",
                plantId, newLevel, coinsAwarded);
        }
    }

    /// <summary>
    /// Records a reading day for the plant if the session was at least 15 minutes long
    /// and no reading day has been recorded for this plant today.
    /// </summary>
    public async Task RecordReadingDayAsync(Guid plantId, DateTime sessionDate, int sessionMinutes, CancellationToken ct = default)
    {
        if (sessionMinutes < 15)
            return;

        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            return;

        await RefreshPlantStatusAsync(plant, ct);

        if (plant.Status == PlantStatus.Dead)
            return;

        var sessionDay = sessionDate.Date;

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

        // Defer coin persistence until plant save succeeds to avoid charging for a failed level-up
        int coinsAwarded = 0;
        if (newLevel > plant.CurrentLevel)
        {
            int levelsGained = newLevel - plant.CurrentLevel;
            plant.CurrentLevel = newLevel;
            coinsAwarded = levelsGained * 100;
        }

        try
        {
            await _unitOfWork.UserPlants.UpdateAsync(plant);
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

    public async Task<bool> CanLevelUpAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            return false;

        await RefreshPlantStatusAsync(plant, ct);

        if (plant.Status == PlantStatus.Dead)
            return false;

        return PlantGrowthCalculator.CanLevelUp(
            plant.CurrentLevel,
            plant.Experience,
            plant.Species.GrowthRate,
            plant.Species.MaxLevel
        );
    }

    public async Task LevelUpAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new EntityNotFoundException(typeof(UserPlant), plantId);

        await RefreshPlantStatusAsync(plant, ct);

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot level up a dead plant");

        if (!await CanLevelUpAsync(plantId, ct))
            throw new InvalidOperationException("Plant cannot level up yet");

        plant.CurrentLevel++;

        try
        {
            await _unitOfWork.UserPlants.UpdateAsync(plant);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict leveling up plant {PlantId}", plantId);
            throw new ConcurrencyException($"Plant with ID {plantId} was modified by another user. Please reload and try again.", ex);
        }
    }

    public async Task PurchaseLevelAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new EntityNotFoundException(typeof(UserPlant), plantId);

        await RefreshPlantStatusAsync(plant, ct);

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot level up a dead plant");

        if (plant.CurrentLevel >= plant.Species.MaxLevel)
            throw new InvalidOperationException("Plant is already at max level");

        int cost = (plant.CurrentLevel + 1) * 100;

        await _settingsProvider.SpendCoinsAsync(cost, ct);

        // _settingsProvider and _unitOfWork use separate DbContexts: if the plant save fails
        // after coins are deducted, refund explicitly to avoid charging for a level not received.
        try
        {
            plant.CurrentLevel++;
            await _unitOfWork.UserPlants.UpdateAsync(plant);
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
        var species = await _unitOfWork.PlantSpecies.GetByIdAsync(speciesId);
        if (species == null)
            throw new EntityNotFoundException(typeof(PlantSpecies), speciesId);

        if (!species.IsAvailable)
            throw new InvalidOperationException("Plant species is not available for purchase");

        int cost = await GetPlantCostAsync(speciesId, ct);

        await _settingsProvider.SpendCoinsAsync(cost, ct);

        // _settingsProvider and _unitOfWork use separate DbContexts: if the plant save fails
        // after coins are deducted, refund explicitly to avoid charging for a plant not received.
        UserPlant result;
        try
        {
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

            result = await _unitOfWork.UserPlants.AddAsync(plant);
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

        // Increment after successful purchase: a pre-save increment would raise dynamic price
        // even if the plant never existed. A failure here is self-correcting (slightly off price
        // for the next plant).
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

    /// <summary>Update all plant statuses based on last watered time.</summary>
    public async Task UpdatePlantStatusesAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        await RefreshPlantStatusesAsync(plants, ct);
    }

    /// <summary>
    /// Get plants needing water soon (within 6 hours).
    /// Honours the global growth multiplier so notifications align with UI thirst timeline.
    /// </summary>
    public async Task<IReadOnlyList<UserPlant>> GetPlantsNeedingWaterAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        await RefreshPlantStatusesAsync(plants, ct);

        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        return plants
            .Where(p => p.Status != PlantStatus.Dead)
            .Where(p => PlantGrowthCalculator.NeedsWateringSoon(p.LastWatered, p.Species.WaterIntervalDays, growthMultiplier))
            .ToList();
    }

    /// <summary>Get available species for purchase based on user level.</summary>
    public async Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default)
    {
        var species = await _unitOfWork.PlantSpecies.FindAsync(s => s.IsAvailable && s.UnlockLevel <= userLevel, ct);
        return species.OrderBy(s => s.BaseCost).ToList();
    }

    /// <summary>
    /// Calculate total XP boost percentage from all owned plants.
    /// Delegates to <see cref="SpecialAbilityResolver.CalculateAggregatedPlantBoost"/> so UI
    /// and XP-award use the same calculation.
    /// </summary>
    public async Task<decimal> CalculateTotalXpBoostAsync(CancellationToken ct = default)
    {
        var allPlants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        await RefreshPlantStatusesAsync(allPlants, ct);
        bool hasStoryHeart = await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart, ct);
        return SpecialAbilityResolver.CalculateAggregatedPlantBoost(allPlants, hasStoryHeart);
    }

    /// <summary>Dynamic cost: BaseCost + (PlantsPurchased × 200).</summary>
    public async Task<int> GetPlantCostAsync(Guid speciesId, CancellationToken ct = default)
    {
        var species = await _unitOfWork.PlantSpecies.GetByIdAsync(speciesId);
        if (species == null)
            throw new EntityNotFoundException(typeof(PlantSpecies), speciesId);

        int plantsPurchased = await _settingsProvider.GetPlantsPurchasedAsync(ct);
        int dynamicCost = species.BaseCost + (plantsPurchased * 200);

        return dynamicCost;
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
            await _unitOfWork.UserPlants.UpdateAsync(plant);
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task RefreshPlantStatusAsync(UserPlant plant, CancellationToken ct)
    {
        // Phoenix ownership is cross-plant: load all plants even when refreshing one
        var allPlants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        bool userOwnsPhoenix = SpecialAbilityResolver.AnyAlivePlantHasAbility(allPlants, SpecialAbilityKeys.EternalPhoenix);
        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        if (!TryApplyCurrentPlantStatus(plant, userOwnsPhoenix, growthMultiplier))
            return;

        await _unitOfWork.UserPlants.UpdateAsync(plant);
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

        // Phoenix protection: while a Phoenix-Bonsai is owned, no plant can die.
        // For the Phoenix itself, reset LastWatered to avoid re-entering this branch every refresh.
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
