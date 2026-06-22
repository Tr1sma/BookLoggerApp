using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository interface for ReadingGoal entity with specific operations.
/// </summary>
public interface IReadingGoalRepository : IRepository<ReadingGoal>
{
    Task<IEnumerable<ReadingGoal>> GetActiveGoalsAsync(CancellationToken ct = default);
    Task<IEnumerable<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default);
    Task<IEnumerable<ReadingGoal>> GetGoalsInRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
}
