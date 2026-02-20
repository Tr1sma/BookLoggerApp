using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using BookLoggerApp.Core.Exceptions;
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
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlantService> _logger;
    private const string SpeciesCacheKey = "AllPlantSpecies";

    public PlantService(
        IUnitOfWork unitOfWork,
        IAppSettingsProvider settingsProvider,
        IMemoryCache cache,
        ILogger<PlantService> logger)
    {
        _unitOfWork = unitOfWork;
        _settingsProvider = settingsProvider;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserPlant>> GetAllAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        return plants.ToList();
    }

    public async Task<UserPlant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(id);
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
            await _unitOfWork.UserPlants.DeleteAsync(plant);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.UserPlants.GetActivePlantAsync();
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

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot water a dead plant");

        plant.LastWatered = DateTime.UtcNow;

        // Recalculate status
        plant.Status = PlantGrowthCalculator.CalculatePlantStatus(plant.LastWatered, plant.Species.WaterIntervalDays);

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

        // Update level if changed
        if (newLevel > plant.CurrentLevel)
        {
            int levelsGained = newLevel - plant.CurrentLevel;
            plant.CurrentLevel = newLevel;

            // Award 100 coins per level gained
            int coinsAwarded = levelsGained * 100;
            await _settingsProvider.AddCoinsAsync(coinsAwarded, ct);
            _logger.LogInformation("Plant {PlantId} leveled up to {Level}, awarded {Coins} coins",
                plantId, newLevel, coinsAwarded);
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

        // Calculate new level based on reading days
        int newLevel = PlantGrowthCalculator.CalculateLevelFromReadingDays(
            plant.ReadingDaysCount,
            plant.Species.GrowthRate,
            plant.Species.MaxLevel
        );

        // Update level if changed
        if (newLevel > plant.CurrentLevel)
        {
            int levelsGained = newLevel - plant.CurrentLevel;
            plant.CurrentLevel = newLevel;

            // Award 100 coins per level gained
            int coinsAwarded = levelsGained * 100;
            await _settingsProvider.AddCoinsAsync(coinsAwarded, ct);
            _logger.LogInformation("Plant {PlantId} leveled up to {Level} after {ReadingDays} reading days, awarded {Coins} coins",
                plantId, newLevel, plant.ReadingDaysCount, coinsAwarded);
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
    }

    public async Task<bool> CanLevelUpAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _unitOfWork.UserPlants.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
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

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot level up a dead plant");

        if (plant.CurrentLevel >= plant.Species.MaxLevel)
            throw new InvalidOperationException("Plant is already at max level");

        // Calculate cost: 100 coins per level
        int cost = (plant.CurrentLevel + 1) * 100;

        // Spend coins (will throw if not enough)
        await _settingsProvider.SpendCoinsAsync(cost, ct);

        // Level up the plant
        plant.CurrentLevel++;
        await _unitOfWork.UserPlants.UpdateAsync(plant);
        await _unitOfWork.SaveChangesAsync(ct);
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

        // Increment PlantsPurchased counter for dynamic pricing
        await _settingsProvider.IncrementPlantsPurchasedAsync(ct);

        // Create the plant
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

        var result = await _unitOfWork.UserPlants.AddAsync(plant);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>
    /// Update all plant statuses based on last watered time.
    /// Called periodically (e.g., when app starts or in background).
    /// </summary>
    public async Task UpdatePlantStatusesAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync();

        foreach (var plant in plants)
        {
            var newStatus = PlantGrowthCalculator.CalculatePlantStatus(
                plant.LastWatered,
                plant.Species.WaterIntervalDays
            );

            if (plant.Status != newStatus)
            {
                plant.Status = newStatus;
                await _unitOfWork.UserPlants.UpdateAsync(plant);
            }
        }

        // Single SaveChanges for all updates - batch optimization
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Get plants that need watering soon (within 6 hours).
    /// </summary>
    public async Task<IReadOnlyList<UserPlant>> GetPlantsNeedingWaterAsync(CancellationToken ct = default)
    {
        var plants = await _unitOfWork.UserPlants.GetUserPlantsAsync();

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
    /// Formula per plant: baseBoost + (levelBonus per level)
    /// Example: StarterSprout at level 5 = 5% + (5 * 0.5%) = 7.5%
    /// Total boost is cumulative across all plants.
    /// </summary>
    public async Task<decimal> CalculateTotalXpBoostAsync(CancellationToken ct = default)
    {
        var allPlants = await _unitOfWork.UserPlants.GetUserPlantsAsync();
        var plants = allPlants.Where(p => p.Status != PlantStatus.Dead).ToList();

        if (!plants.Any())
            return 0m;

        decimal totalBoost = 0m;

        foreach (var plant in plants)
        {
            if (plant.Species == null)
                continue;

            // Calculate boost for this plant
            // Formula: baseBoost + (currentLevel * (baseBoost / maxLevel))
            decimal baseBoost = plant.Species.XpBoostPercentage;
            decimal levelBonus = plant.Species.MaxLevel > 0
                ? plant.CurrentLevel * (plant.Species.XpBoostPercentage / plant.Species.MaxLevel)
                : 0m;
            decimal plantBoost = baseBoost + levelBonus;

            totalBoost += plantBoost;
        }

        return totalBoost;
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
}
