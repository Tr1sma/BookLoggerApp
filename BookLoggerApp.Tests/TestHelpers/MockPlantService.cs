using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Mock implementation of IPlantService for testing purposes.
/// Returns default/empty values for all operations.
/// </summary>
public class MockPlantService : IPlantService
{
    // Plant CRUD
    public Task<IReadOnlyList<UserPlant>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<UserPlant>>(Array.Empty<UserPlant>());
    }

    public Task<UserPlant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult<UserPlant?>(null);
    }

    public Task<UserPlant> AddAsync(UserPlant plant, CancellationToken ct = default)
    {
        return Task.FromResult(plant);
    }

    public Task UpdateAsync(UserPlant plant, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    // Active Plant
    public Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default)
    {
        return Task.FromResult<UserPlant?>(null);
    }

    public Task SetActivePlantAsync(Guid plantId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    // Plant Species
    public Task<IReadOnlyList<PlantSpecies>> GetAllSpeciesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PlantSpecies>>(Array.Empty<PlantSpecies>());
    }

    public Task<PlantSpecies?> GetSpeciesByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult<PlantSpecies?>(null);
    }

    // Growth & Care
    public Task WaterPlantAsync(Guid plantId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task AddExperienceAsync(Guid plantId, int xp, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> CanLevelUpAsync(Guid plantId, CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    public Task LevelUpAsync(Guid plantId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task PurchaseLevelAsync(Guid plantId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task RecordReadingDayAsync(Guid plantId, DateTime sessionDate, int sessionMinutes, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    // Purchase
    public Task<UserPlant> PurchasePlantAsync(Guid speciesId, string name, CancellationToken ct = default)
    {
        return Task.FromResult(new UserPlant { Id = Guid.NewGuid(), Name = name });
    }

    // Status Management
    public Task UpdatePlantStatusesAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserPlant>> GetPlantsNeedingWaterAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<UserPlant>>(Array.Empty<UserPlant>());
    }

    // Shop
    public Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PlantSpecies>>(Array.Empty<PlantSpecies>());
    }

    // XP Boost System
    public Task<decimal> CalculateTotalXpBoostAsync(CancellationToken ct = default)
    {
        return Task.FromResult(0m);
    }

    public Task<int> GetPlantCostAsync(Guid speciesId, CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }
}
