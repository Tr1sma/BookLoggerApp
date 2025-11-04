using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository interface for UserPlant entity with specific operations.
/// </summary>
public interface IUserPlantRepository : IRepository<UserPlant>
{
    Task<UserPlant?> GetActivePlantAsync();
    Task<IEnumerable<UserPlant>> GetUserPlantsAsync();
    Task<UserPlant?> GetPlantWithSpeciesAsync(Guid id);
}
