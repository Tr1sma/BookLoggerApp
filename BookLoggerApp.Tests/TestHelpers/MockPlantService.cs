using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

public class MockPlantService : IPlantService
{
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

    public Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default)
    {
        return Task.FromResult<UserPlant?>(null);
    }

    public Task SetActivePlantAsync(Guid plantId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlantSpecies>> GetAllSpeciesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PlantSpecies>>(Array.Empty<PlantSpecies>());
    }

    public Task<PlantSpecies?> GetSpeciesByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult<PlantSpecies?>(null);
    }

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

    public Task<UserPlant> PurchasePlantAsync(Guid speciesId, string name, CancellationToken ct = default)
    {
        return Task.FromResult(new UserPlant { Id = Guid.NewGuid(), Name = name });
    }

    public Task UpdatePlantStatusesAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserPlant>> GetPlantsNeedingWaterAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<UserPlant>>(Array.Empty<UserPlant>());
    }

    public Task<IReadOnlyList<PlantSpecies>> GetAvailableSpeciesAsync(int userLevel, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PlantSpecies>>(Array.Empty<PlantSpecies>());
    }

    public Task<decimal> CalculateTotalXpBoostAsync(CancellationToken ct = default)
    {
        return Task.FromResult(0m);
    }

    public Task<int> GetPlantCostAsync(Guid speciesId, CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }
}
