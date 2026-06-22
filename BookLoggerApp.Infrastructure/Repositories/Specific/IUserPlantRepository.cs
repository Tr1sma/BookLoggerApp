using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository interface for UserPlant entity with specific operations.
/// </summary>
public interface IUserPlantRepository : IRepository<UserPlant>
{
    Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default);
    Task<IEnumerable<UserPlant>> GetUserPlantsAsync(CancellationToken ct = default);
    Task<UserPlant?> GetPlantWithSpeciesAsync(Guid id, CancellationToken ct = default);
}
