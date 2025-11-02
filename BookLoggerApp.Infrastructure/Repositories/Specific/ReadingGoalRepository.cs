using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository implementation for ReadingGoal entity.
/// </summary>
public class ReadingGoalRepository : Repository<ReadingGoal>, IReadingGoalRepository
{
    public ReadingGoalRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ReadingGoal>> GetActiveGoalsAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(rg => !rg.IsCompleted && rg.EndDate >= now)
            .OrderBy(rg => rg.EndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReadingGoal>> GetCompletedGoalsAsync()
    {
        return await _dbSet
            .Where(rg => rg.IsCompleted)
            .OrderByDescending(rg => rg.EndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReadingGoal>> GetGoalsInRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(rg => rg.StartDate <= endDate && rg.EndDate >= startDate)
            .OrderBy(rg => rg.StartDate)
            .ToListAsync();
    }
}
