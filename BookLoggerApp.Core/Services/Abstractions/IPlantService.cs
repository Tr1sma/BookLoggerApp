using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing user plants and plant growth mechanics.
/// </summary>
public interface IPlantService
{
    // Plant CRUD
    Task<IReadOnlyList<UserPlant>> GetAllAsync(CancellationToken ct = default);
    Task<UserPlant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserPlant> AddAsync(UserPlant plant, CancellationToken ct = default);
    Task UpdateAsync(UserPlant plant, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Active Plant
    Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default);
    Task SetActivePlantAsync(Guid plantId, CancellationToken ct = default);

    // Plant Species
    Task<IReadOnlyList<PlantSpecies>> GetAllSpeciesAsync(CancellationToken ct = default);
    Task<PlantSpecies?> GetSpeciesByIdAsync(Guid id, CancellationToken ct = default);

    // Growth & Care
    Task WaterPlantAsync(Guid plantId, CancellationToken ct = default);
    Task AddExperienceAsync(Guid plantId, int xp, CancellationToken ct = default);
    Task<bool> CanLevelUpAsync(Guid plantId, CancellationToken ct = default);
    Task LevelUpAsync(Guid plantId, CancellationToken ct = default);
    Task PurchaseLevelAsync(Guid plantId, CancellationToken ct = default);

    /// <summary>
    /// Records a reading day for the plant if:
    /// - Session was at least 15 minutes long
    /// - No reading day has been recorded for this plant today
    /// Automatically updates the plant's level based on reading days.
    /// </summary>
    Task RecordReadingDayAsync(Guid plantId, DateTime sessionDate, int sessionMinutes, CancellationToken ct = default);

    // Purchase
    Task<UserPlant> PurchasePlantAsync(Guid speciesId, string name, CancellationToken ct = default);

    // Status Management
    Task UpdatePlantStatusesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserPlant>> GetPlantsNeedingWaterAsync(CancellationToken ct = default);

    // Shop
    Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default);

    // XP Boost System
    Task<decimal> CalculateTotalXpBoostAsync(CancellationToken ct = default);
    Task<int> GetPlantCostAsync(Guid speciesId, CancellationToken ct = default);
}
