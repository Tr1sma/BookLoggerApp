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

    public async Task<UserPlant?> GetActivePlantAsync(CancellationToken ct = default)
    {
        // Hidden-by-entitlement plants (e.g. prestige after a Premium→Free downgrade) are paid
        // content and must never surface as the active plant (SEC-11).
        return await _dbSet
            .Include(up => up.Species)
            .Where(up => !up.IsHiddenByEntitlement)
            .FirstOrDefaultAsync(up => up.IsActive, ct);
    }

    public async Task<IEnumerable<UserPlant>> GetUserPlantsAsync(CancellationToken ct = default)
    {
        // Filter out hidden-by-entitlement plants so downgraded users don't see/count them (SEC-11).
        // INK-10: intentionally TRACKED — PlantService.GetAllAsync mutates these via
        // RefreshPlantStatusesAsync and persists; AsNoTracking would silently drop those updates.
        return await _dbSet
            .Include(up => up.Species)
            .Where(up => !up.IsHiddenByEntitlement)
            .OrderByDescending(up => up.PlantedAt)
            .ToListAsync(ct);
    }

    public async Task<UserPlant?> GetPlantWithSpeciesAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(up => up.Species)
            .FirstOrDefaultAsync(up => up.Id == id, ct);
    }
}
