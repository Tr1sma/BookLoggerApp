using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories.Specific;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing reading goals.
/// </summary>
public class GoalService : IGoalService
{
    private readonly IReadingGoalRepository _goalRepository;

    public GoalService(IReadingGoalRepository goalRepository)
    {
        _goalRepository = goalRepository;
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetAllAsync(CancellationToken ct = default)
    {
        var goals = await _goalRepository.GetAllAsync();
        return goals.ToList();
    }

    public async Task<ReadingGoal?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _goalRepository.GetByIdAsync(id);
    }

    public async Task<ReadingGoal> AddAsync(ReadingGoal goal, CancellationToken ct = default)
    {
        return await _goalRepository.AddAsync(goal);
    }

    public async Task UpdateAsync(ReadingGoal goal, CancellationToken ct = default)
    {
        await _goalRepository.UpdateAsync(goal);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var goal = await _goalRepository.GetByIdAsync(id);
        if (goal != null)
        {
            await _goalRepository.DeleteAsync(goal);
        }
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetActiveGoalsAsync(CancellationToken ct = default)
    {
        var goals = await _goalRepository.GetActiveGoalsAsync();
        return goals.ToList();
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default)
    {
        var goals = await _goalRepository.GetCompletedGoalsAsync();
        return goals.ToList();
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetGoalsByTypeAsync(GoalType type, CancellationToken ct = default)
    {
        var goals = await _goalRepository.FindAsync(g => g.Type == type);
        return goals.ToList();
    }

    public async Task UpdateGoalProgressAsync(Guid goalId, int progress, CancellationToken ct = default)
    {
        var goal = await _goalRepository.GetByIdAsync(goalId);
        if (goal == null)
            throw new ArgumentException("Goal not found", nameof(goalId));

        goal.Current = progress;

        // Auto-complete if target reached
        if (goal.Current >= goal.Target)
        {
            goal.IsCompleted = true;
        }

        await _goalRepository.UpdateAsync(goal);
    }

    public async Task CheckAndCompleteGoalsAsync(CancellationToken ct = default)
    {
        var activeGoals = await _goalRepository.GetActiveGoalsAsync();

        foreach (var goal in activeGoals)
        {
            if (goal.Current >= goal.Target)
            {
                goal.IsCompleted = true;
                await _goalRepository.UpdateAsync(goal);
            }
        }
    }
}
