using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services.Helpers;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing user plants.
/// </summary>
public class PlantService : IPlantService
{
    private readonly IUserPlantRepository _plantRepository;
    private readonly IRepository<PlantSpecies> _speciesRepository;
    private readonly AppDbContext _context;

    public PlantService(
        IUserPlantRepository plantRepository,
        IRepository<PlantSpecies> speciesRepository,
        AppDbContext context)
    {
        _plantRepository = plantRepository;
        _speciesRepository = speciesRepository;
        _context = context;
    }

    public async Task<IReadOnlyList<UserPlant>> GetAllAsync(CancellationToken ct = default)
    {
        var plants = await _plantRepository.GetUserPlantsAsync();
        return plants.ToList();
    }

    public async Task<UserPlant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _plantRepository.GetPlantWithSpeciesAsync(id);
    }

    public async Task<UserPlant> AddAsync(UserPlant plant, CancellationToken ct = default)
    {
        if (plant.PlantedAt == default)
            plant.PlantedAt = DateTime.UtcNow;

        if (plant.LastWatered == default)
            plant.LastWatered = DateTime.UtcNow;

        return await _plantRepository.AddAsync(plant);
    }

    public async Task UpdateAsync(UserPlant plant, CancellationToken ct = default)
    {
        await _plantRepository.UpdateAsync(plant);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plant = await _plantRepository.GetByIdAsync(id);
        if (plant != null)
        {
            await _plantRepository.DeleteAsync(plant);
        }
    }

    public async Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default)
    {
        return await _plantRepository.GetActivePlantAsync();
    }

    public async Task SetActivePlantAsync(Guid plantId, CancellationToken ct = default)
    {
        // Deactivate all plants
        var allPlants = await _plantRepository.GetAllAsync();
        foreach (var plant in allPlants)
        {
            plant.IsActive = false;
            await _plantRepository.UpdateAsync(plant);
        }

        // Activate the selected plant
        var selectedPlant = await _plantRepository.GetByIdAsync(plantId);
        if (selectedPlant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        selectedPlant.IsActive = true;
        await _plantRepository.UpdateAsync(selectedPlant);
    }

    public async Task<IReadOnlyList<PlantSpecies>> GetAllSpeciesAsync(CancellationToken ct = default)
    {
        var species = await _speciesRepository.GetAllAsync();
        return species.ToList();
    }

    public async Task<PlantSpecies?> GetSpeciesByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _speciesRepository.GetByIdAsync(id);
    }

    public async Task WaterPlantAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _plantRepository.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot water a dead plant");

        plant.LastWatered = DateTime.UtcNow;

        // Recalculate status
        plant.Status = PlantGrowthCalculator.CalculatePlantStatus(plant.LastWatered, plant.Species.WaterIntervalDays);

        await _plantRepository.UpdateAsync(plant);
    }

    public async Task AddExperienceAsync(Guid plantId, int xp, CancellationToken ct = default)
    {
        var plant = await _plantRepository.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

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
            plant.CurrentLevel = newLevel;

            // TODO: Award coins for level up (requires IStatsService or AppSettings repository)
            // settings.Coins += 100;
        }

        await _plantRepository.UpdateAsync(plant);
    }

    public async Task<bool> CanLevelUpAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _plantRepository.GetPlantWithSpeciesAsync(plantId);
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
        var plant = await _plantRepository.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        if (!await CanLevelUpAsync(plantId, ct))
            throw new InvalidOperationException("Plant cannot level up yet");

        plant.CurrentLevel++;

        await _plantRepository.UpdateAsync(plant);
    }

    public async Task PurchaseLevelAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _plantRepository.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        if (plant.Status == PlantStatus.Dead)
            throw new InvalidOperationException("Cannot level up a dead plant");

        if (plant.CurrentLevel >= plant.Species.MaxLevel)
            throw new InvalidOperationException("Plant is already at max level");

        // Calculate cost: 100 coins per level
        int cost = (plant.CurrentLevel + 1) * 100;

        // Get AppSettings
        var settings = await _context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        // Check if user has enough coins
        if (settings.Coins < cost)
            throw new InvalidOperationException($"Not enough coins. Need {cost}, have {settings.Coins}");

        // Deduct coins
        settings.Coins -= cost;
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        // Level up the plant
        plant.CurrentLevel++;
        await _plantRepository.UpdateAsync(plant);
    }

    public async Task<UserPlant> PurchasePlantAsync(Guid speciesId, string name, CancellationToken ct = default)
    {
        var species = await _speciesRepository.GetByIdAsync(speciesId);
        if (species == null)
            throw new ArgumentException("Plant species not found", nameof(speciesId));

        if (!species.IsAvailable)
            throw new InvalidOperationException("Plant species is not available for purchase");

        // Get AppSettings
        var settings = await _context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        // Calculate dynamic cost
        int cost = await GetPlantCostAsync(speciesId, ct);

        // Check if user has enough coins
        if (settings.Coins < cost)
            throw new InvalidOperationException($"Not enough coins. Need {cost}, have {settings.Coins}");

        // Deduct coins
        settings.Coins -= cost;

        // Increment PlantsPurchased counter for dynamic pricing
        settings.PlantsPurchased++;

        // Update settings
        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

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

        return await _plantRepository.AddAsync(plant);
    }

    /// <summary>
    /// Update all plant statuses based on last watered time.
    /// Called periodically (e.g., when app starts or in background).
    /// </summary>
    public async Task UpdatePlantStatusesAsync(CancellationToken ct = default)
    {
        var plants = await _context.UserPlants
            .Include(p => p.Species)
            .ToListAsync(ct);

        foreach (var plant in plants)
        {
            var newStatus = PlantGrowthCalculator.CalculatePlantStatus(
                plant.LastWatered,
                plant.Species.WaterIntervalDays
            );

            if (plant.Status != newStatus)
            {
                plant.Status = newStatus;
                await _plantRepository.UpdateAsync(plant);
            }
        }
    }

    /// <summary>
    /// Get plants that need watering soon (within 6 hours).
    /// </summary>
    public async Task<IReadOnlyList<UserPlant>> GetPlantsNeedingWaterAsync(CancellationToken ct = default)
    {
        var plants = await _context.UserPlants
            .Include(p => p.Species)
            .Where(p => p.Status != PlantStatus.Dead)
            .ToListAsync(ct);

        return plants
            .Where(p => PlantGrowthCalculator.NeedsWateringSoon(p.LastWatered, p.Species.WaterIntervalDays))
            .ToList();
    }

    /// <summary>
    /// Get available species for purchase based on user level.
    /// </summary>
    public async Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default)
    {
        return await _context.PlantSpecies
            .Where(s => s.IsAvailable && s.UnlockLevel <= userLevel)
            .OrderBy(s => s.BaseCost)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Calculate the total XP boost percentage from all owned plants.
    /// Formula per plant: baseBoost + (levelBonus per level)
    /// Example: StarterSprout at level 5 = 5% + (5 * 0.5%) = 7.5%
    /// Total boost is cumulative across all plants.
    /// </summary>
    public async Task<decimal> CalculateTotalXpBoostAsync(CancellationToken ct = default)
    {
        var plants = await _context.UserPlants
            .Include(p => p.Species)
            .ToListAsync(ct);

        if (!plants.Any())
            return 0m;

        decimal totalBoost = 0m;

        foreach (var plant in plants)
        {
            if (plant.Species == null)
                continue;

            // Dead plants don't contribute to XP boost
            if (plant.Status == PlantStatus.Dead)
                continue;

            // Calculate boost for this plant
            // Formula: baseBoost + (currentLevel * (baseBoost / maxLevel))
            decimal baseBoost = plant.Species.XpBoostPercentage;
            decimal levelBonus = plant.CurrentLevel * (plant.Species.XpBoostPercentage / plant.Species.MaxLevel);
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
        var species = await _speciesRepository.GetByIdAsync(speciesId);
        if (species == null)
            throw new ArgumentException("Plant species not found", nameof(speciesId));

        // Get PlantsPurchased from AppSettings
        var settings = await _context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        int plantsPurchased = settings.PlantsPurchased;

        // Calculate dynamic price
        int dynamicCost = species.BaseCost + (plantsPurchased * 200);

        return dynamicCost;
    }
}
