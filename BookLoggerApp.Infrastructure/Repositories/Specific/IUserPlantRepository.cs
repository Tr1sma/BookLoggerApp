using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

public interface IUserPlantRepository : IRepository<UserPlant>
{
    Task<UserPlant?> GetActivePlantAsync();
    Task<IEnumerable<UserPlant>> GetUserPlantsAsync();
    Task<UserPlant?> GetPlantWithSpeciesAsync(Guid id);
}
