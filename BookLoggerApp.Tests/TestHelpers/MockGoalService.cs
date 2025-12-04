using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Mock implementation of IGoalService for testing purposes.
/// </summary>
public class MockGoalService : IGoalService
{
    public event EventHandler? GoalsChanged;

    public void NotifyGoalsChanged()
    {
        GoalsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<IReadOnlyList<ReadingGoal>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ReadingGoal>>(Array.Empty<ReadingGoal>());
    }

    public Task<ReadingGoal?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult<ReadingGoal?>(null);
    }

    public Task<ReadingGoal> AddAsync(ReadingGoal goal, CancellationToken ct = default)
    {
        return Task.FromResult(goal);
    }

    public Task UpdateAsync(ReadingGoal goal, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReadingGoal>> GetActiveGoalsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ReadingGoal>>(Array.Empty<ReadingGoal>());
    }

    public Task<IReadOnlyList<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ReadingGoal>>(Array.Empty<ReadingGoal>());
    }

    public Task<IReadOnlyList<ReadingGoal>> GetGoalsByTypeAsync(GoalType type, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ReadingGoal>>(Array.Empty<ReadingGoal>());
    }

    public Task UpdateGoalProgressAsync(Guid goalId, int progress, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task CheckAndCompleteGoalsAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
