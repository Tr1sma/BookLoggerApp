using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Helpers;
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

    public async Task<IEnumerable<ReadingGoal>> GetActiveGoalsAsync(CancellationToken ct = default)
    {
        // Compare against local midnight, not UtcNow: EndDate holds local calendar midnight, so
        // UtcNow would drop goals early for positive-UTC users. Cutoff in GoalActivityHelper so
        // app/widget can't drift (INK-06).
        var todayLocalMidnight = GoalActivityHelper.ActiveCutoff(DateTime.Now);
        return await _dbSet
            .AsNoTracking()
            .Where(rg => !rg.IsCompleted && rg.EndDate >= todayLocalMidnight)
            .OrderBy(rg => rg.EndDate)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(rg => rg.IsCompleted)
            .OrderByDescending(rg => rg.EndDate)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ReadingGoal>> GetGoalsInRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(rg => rg.StartDate <= endDate && rg.EndDate >= startDate)
            .OrderBy(rg => rg.StartDate)
            .ToListAsync(ct);
    }
}
