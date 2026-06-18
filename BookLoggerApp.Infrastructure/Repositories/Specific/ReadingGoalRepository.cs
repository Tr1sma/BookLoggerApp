using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

public class ReadingGoalRepository : Repository<ReadingGoal>, IReadingGoalRepository
{
    public ReadingGoalRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ReadingGoal>> GetActiveGoalsAsync()
    {
        // EndDate is Kind=Unspecified local midnight; UtcNow would expire goals early in positive-UTC timezones
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
