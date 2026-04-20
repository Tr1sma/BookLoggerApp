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
        // Compare against today's local midnight, not DateTime.UtcNow. EndDate is stored
        // with ticks that represent the user's local calendar midnight (the UI date picker
        // produces Kind=Unspecified values), so using UtcNow flips goals off the "active"
        // list several hours before local midnight for users in positive-UTC timezones.
        var todayLocalMidnight = DateTime.Now.Date;
        return await _dbSet
            .AsNoTracking()
            .Where(rg => !rg.IsCompleted && rg.EndDate >= todayLocalMidnight)
            .OrderBy(rg => rg.EndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReadingGoal>> GetCompletedGoalsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(rg => rg.IsCompleted)
            .OrderByDescending(rg => rg.EndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReadingGoal>> GetGoalsInRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(rg => rg.StartDate <= endDate && rg.EndDate >= startDate)
            .OrderBy(rg => rg.StartDate)
            .ToListAsync();
    }
}
