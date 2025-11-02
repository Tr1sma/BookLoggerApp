using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository implementation for UserPlant entity.
/// </summary>
public class UserPlantRepository : Repository<UserPlant>, IUserPlantRepository
{
    public UserPlantRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<UserPlant?> GetActivePlantAsync()
    {
        return await _dbSet
            .Include(up => up.Species)
            .FirstOrDefaultAsync(up => up.IsActive);
    }

    public async Task<IEnumerable<UserPlant>> GetUserPlantsAsync()
    {
        return await _dbSet
            .Include(up => up.Species)
            .OrderByDescending(up => up.PlantedAt)
            .ToListAsync();
    }

    public async Task<UserPlant?> GetPlantWithSpeciesAsync(Guid id)
    {
        return await _dbSet
            .Include(up => up.Species)
            .FirstOrDefaultAsync(up => up.Id == id);
    }
}
