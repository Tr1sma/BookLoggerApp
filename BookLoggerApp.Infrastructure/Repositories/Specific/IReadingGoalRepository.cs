using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

public interface IReadingGoalRepository : IRepository<ReadingGoal>
{
    Task<IEnumerable<ReadingGoal>> GetActiveGoalsAsync();
    Task<IEnumerable<ReadingGoal>> GetCompletedGoalsAsync();
    Task<IEnumerable<ReadingGoal>> GetGoalsInRangeAsync(DateTime startDate, DateTime endDate);
}
