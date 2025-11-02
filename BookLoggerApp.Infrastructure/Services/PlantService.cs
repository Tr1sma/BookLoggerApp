using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Infrastructure.Repositories;

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
        var plant = await _plantRepository.GetByIdAsync(plantId);
        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        plant.LastWatered = DateTime.UtcNow;
        await _plantRepository.UpdateAsync(plant);
    }

    public async Task AddExperienceAsync(Guid plantId, int xp, CancellationToken ct = default)
    {
        var plant = await _plantRepository.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        plant.Experience += xp;

        // Check if plant can level up
        while (await CanLevelUpAsync(plantId, ct))
        {
            await LevelUpAsync(plantId, ct);
        }

        await _plantRepository.UpdateAsync(plant);
    }

    public async Task<bool> CanLevelUpAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _plantRepository.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            return false;

        // Check if not at max level
        if (plant.CurrentLevel >= plant.Species.MaxLevel)
            return false;

        // Calculate XP needed for next level
        int xpForNextLevel = CalculateXpForLevel(plant.CurrentLevel + 1);
        return plant.Experience >= xpForNextLevel;
    }

    public async Task LevelUpAsync(Guid plantId, CancellationToken ct = default)
    {
        var plant = await _plantRepository.GetPlantWithSpeciesAsync(plantId);
        if (plant == null)
            throw new ArgumentException("Plant not found", nameof(plantId));

        if (!await CanLevelUpAsync(plantId, ct))
            throw new InvalidOperationException("Plant cannot level up yet");

        // Deduct XP and increase level
        int xpForCurrentLevel = CalculateXpForLevel(plant.CurrentLevel + 1);
        plant.Experience -= xpForCurrentLevel;
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

        // TODO: Deduct coins from AppSettings (requires IAppSettingsService)

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

    private int CalculateXpForLevel(int level)
    {
        // Exponential growth: Level 2 = 100 XP, Level 3 = 250 XP, etc.
        return (int)(100 * Math.Pow(1.5, level - 2));
    }
}
