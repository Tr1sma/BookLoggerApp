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
        // Hidden-by-entitlement plants (e.g. prestige plants after a Premium→Free downgrade)
        // must never surface as the active plant — they are paid content (CODE_REVIEW SEC-11).
        return await _dbSet
            .Include(up => up.Species)
            .Where(up => !up.IsHiddenByEntitlement)
            .FirstOrDefaultAsync(up => up.IsActive);
    }

    public async Task<IEnumerable<UserPlant>> GetUserPlantsAsync()
    {
        // Filter out hidden-by-entitlement plants so downgraded users neither see them in the
        // garden nor have them counted in boost/status math (CODE_REVIEW SEC-11), mirroring
        // the IsHiddenByEntitlement filter already applied in ShelfService.
        return await _dbSet
            .Include(up => up.Species)
            .Where(up => !up.IsHiddenByEntitlement)
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
