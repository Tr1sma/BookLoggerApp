using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing reading goals and tracking progress.
/// </summary>
public interface IGoalService
{
    // Goal CRUD
    Task<IReadOnlyList<ReadingGoal>> GetAllAsync(CancellationToken ct = default);
    Task<ReadingGoal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ReadingGoal> AddAsync(ReadingGoal goal, CancellationToken ct = default);
    Task UpdateAsync(ReadingGoal goal, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Query Goals
    Task<IReadOnlyList<ReadingGoal>> GetActiveGoalsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReadingGoal>> GetGoalsByTypeAsync(GoalType type, CancellationToken ct = default);

    // Goal Progress Tracking
    Task UpdateGoalProgressAsync(Guid goalId, int progress, CancellationToken ct = default);
    Task CheckAndCompleteGoalsAsync(CancellationToken ct = default);
}
