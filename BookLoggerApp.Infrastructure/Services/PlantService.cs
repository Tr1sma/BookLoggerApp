using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services.Helpers;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing user plants with caching support.
/// </summary>
public class PlantService : IPlantService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IDecorationService _decorationService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlantService> _logger;
    private const string SpeciesCacheKey = "AllPlantSpecies";

    public PlantService(
        IUnitOfWork unitOfWork,
        IAppSettingsProvider settingsProvider,
        IDecorationService decorationService,
        IMemoryCache cache,
        ILogger<PlantService> logger)
    {
        _unitOfWork = unitOfWork;
        _settingsProvider = settingsProvider;
        _decorationService = decorationService;
        _cache = cache;
        _logger = logger;
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
        // Validate that the target plant exists
        var exists = await _unitOfWork.UserPlants.ExistsAsync(p => p.Id == plantId, ct);
        if (!exists)
            throw new EntityNotFoundException(typeof(UserPlant), plantId);

        // Get all plants and update their active status
        // Note: Using Load-Update-Save pattern instead of ExecuteUpdateAsync for compatibility with InMemory provider in tests
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
        // Try to get cached species
        if (_cache.TryGetValue(SpeciesCacheKey, out List<PlantSpecies>? cached))
            return cached!;

        // Load from database if not cached
        var species = await _unitOfWork.PlantSpecies.GetAllAsync(ct);
        var list = species.ToList();

        // Cache for 24 hours (plant species rarely change)
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

        // Recalculate status
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

        // Dead plants don't earn experience (consistent with RecordReadingDayAsync)
        if (plant.Status == PlantStatus.Dead)
            return;

        plant.Experience += xp;

        // Use PlantGrowthCalculator for level calculation
        int newLevel = PlantGrowthCalculator.CalculateLevelFromXp(
            plant.Experience,
            plant.Species.GrowthRate,
            plant.Species.MaxLevel
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
    /// Records a reading day for the plant if:
    /// - Session was at least 15 minutes long
    /// - No reading day has been recorded for this plant today
    /// Automatically updates the plant's level based on reading days.
    /// Formula: 3 reading days = 1 level (at GrowthRate 1.0)
    /// </summary>
    public async Task RecordReadingDayAsync(Guid plantId, DateTime sessionDate, int sessionMinutes, CancellationToken ct = default)
    {
        // Minimum 15 minutes required for a reading day
        if (sessionMinutes < 15)
            return;

        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            return;

        await RefreshPlantStatusAsync(plant, ct);

        // Dead plants don't earn reading days
        if (plant.Status == PlantStatus.Dead)
            return;

        var sessionDay = sessionDate.Date;

        // Check if a reading day was already recorded today for this plant
        if (plant.LastReadingDayRecorded?.Date == sessionDay)
            return;

        // Record the reading day
        plant.ReadingDaysCount++;
        plant.LastReadingDayRecorded = sessionDay;

        double growthMultiplier = await GetGlobalGrowthMultiplierAsync(ct);

        // Calculate new level based on reading days
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

        // Calculate cost: 100 coins per level
        int cost = (plant.CurrentLevel + 1) * 100;

        // Spend coins (will throw if not enough)
        await _settingsProvider.SpendCoinsAsync(cost, ct);

        // Persist the level increase. _settingsProvider and _unitOfWork use separate DbContexts,
        // so we can't wrap both in a single EF Core transaction — if the plant save fails after
        // the coins are already deducted, refund them explicitly so the user isn't charged for a
        // level they never received.
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

        // Calculate dynamic cost
        int cost = await GetPlantCostAsync(speciesId, ct);

        // Spend coins (will throw if not enough)
        await _settingsProvider.SpendCoinsAsync(cost, ct);

        // Create the plant. _settingsProvider and _unitOfWork use separate DbContexts, so a
        // single EF Core transaction can't cover both operations — if the plant save fails
        // after the coins are already deducted, refund them explicitly so the user isn't
        // charged for a plant they never received.
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

        // Increment PlantsPurchased only after the purchase actually succeeded. Previously this
        // ran before the plant save, so a failed save would raise the dynamic price even though
        // no plant existed. A failure here does NOT invalidate the purchase — at worst the next
        // plant is priced as if this one hadn't been bought yet, which is self-correcting.
        try
        {
            await _settingsProvider.IncrementPlantsPurchasedAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plant purchase succeeded but PlantsPurchased counter increment failed for species {SpeciesId}. Dynamic pricing for the next plant may be slightly off.", speciesId);
        }

        return result;
    }

    /// <summary>
    /// Update all plant statuses based on last watered time.
    /// Called periodically (e.g., when app starts or in background).
    /// </summary>
    public async Task UpdatePlantStatusesAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        await RefreshPlantStatusesAsync(plants, ct);
    }

    /// <summary>
    /// Get plants that need watering soon (within 6 hours).
    /// </summary>
    public async Task<IReadOnlyList<UserPlant>> GetPlantsNeedingWaterAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        await RefreshPlantStatusesAsync(plants, ct);

        return plants
            .Where(p => p.Status != PlantStatus.Dead)
            .Where(p => PlantGrowthCalculator.NeedsWateringSoon(p.LastWatered, p.Species.WaterIntervalDays))
            .ToList();
    }

    /// <summary>
    /// Get available species for purchase based on user level.
    /// </summary>
    public async Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default)
    {
        var species = await _unitOfWork.PlantSpecies.FindAsync(s => s.IsAvailable && s.UnlockLevel <= userLevel, ct);
        return species.OrderBy(s => s.BaseCost).ToList();
    }

    /// <summary>
    /// Calculate the total XP boost percentage from all owned plants.
    /// Formula per plant: baseBoost + (levelBonus per level).
    /// Delegates to <see cref="SpecialAbilityResolver.CalculateAggregatedPlantBoost"/>
    /// so UI display and XP-award use the same calculation.
    /// </summary>
    public async Task<decimal> CalculateTotalXpBoostAsync(CancellationToken ct = default)
    {
        var allPlants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        await RefreshPlantStatusesAsync(allPlants, ct);
        bool hasStoryHeart = await _decorationService.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart, ct);
        return SpecialAbilityResolver.CalculateAggregatedPlantBoost(allPlants, hasStoryHeart);
    }

    /// <summary>
    /// Get the dynamic cost for purchasing a plant species.
    /// Formula: BaseCost + (PlantsPurchased × 200)
    /// Example: First plant = 500, second = 700, third = 900
    /// </summary>
    public async Task<int> GetPlantCostAsync(Guid speciesId, CancellationToken ct = default)
    {
        var species = await _unitOfWork.PlantSpecies.GetByIdAsync(speciesId);
        if (species == null)
            throw new EntityNotFoundException(typeof(PlantSpecies), speciesId);

        // Get PlantsPurchased from AppSettings
        int plantsPurchased = await _settingsProvider.GetPlantsPurchasedAsync(ct);

        // Calculate dynamic price
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
        // Phoenix ownership gates a protection rule that applies across all plants, so we
        // load the full set to determine it even when only one plant is being refreshed.
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

        // Phoenix protection: while a Phoenix-Bonsai is owned, no plant in the garden can
        // transition to Dead. The Phoenix itself is always owned (it self-revives), so the
        // same check handles self-revival. For the Phoenix we also reset LastWatered to now
        // so it doesn't re-enter the "would be dead" branch every status refresh.
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
