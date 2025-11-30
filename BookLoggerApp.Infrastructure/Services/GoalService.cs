using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing reading goals.
/// </summary>
public class GoalService : IGoalService
{
    private readonly IUnitOfWork _unitOfWork;

    public GoalService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetAllAsync(CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.GetAllAsync();
        return goals.ToList();
    }

    public async Task<ReadingGoal?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.ReadingGoals.GetByIdAsync(id);
    }

    public async Task<ReadingGoal> AddAsync(ReadingGoal goal, CancellationToken ct = default)
    {
        var result = await _unitOfWork.ReadingGoals.AddAsync(goal);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task UpdateAsync(ReadingGoal goal, CancellationToken ct = default)
    {
        await _unitOfWork.ReadingGoals.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var goal = await _unitOfWork.ReadingGoals.GetByIdAsync(id);
        if (goal != null)
        {
            await _unitOfWork.ReadingGoals.DeleteAsync(goal);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetActiveGoalsAsync(CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.GetActiveGoalsAsync();
        return goals.ToList();
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.GetCompletedGoalsAsync();
        return goals.ToList();
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetGoalsByTypeAsync(GoalType type, CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.FindAsync(g => g.Type == type);
        return goals.ToList();
    }

    public async Task UpdateGoalProgressAsync(Guid goalId, int progress, CancellationToken ct = default)
    {
        var goal = await _unitOfWork.ReadingGoals.GetByIdAsync(goalId);
        if (goal == null)
            throw new EntityNotFoundException(typeof(ReadingGoal), goalId);

        goal.Current = progress;

        // Auto-complete if target reached
        if (goal.Current >= goal.Target)
        {
            goal.IsCompleted = true;
        }

        await _unitOfWork.ReadingGoals.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task CheckAndCompleteGoalsAsync(CancellationToken ct = default)
    {
        var activeGoals = await _unitOfWork.ReadingGoals.GetActiveGoalsAsync();

        foreach (var goal in activeGoals)
        {
            if (goal.Current >= goal.Target)
            {
                goal.IsCompleted = true;
                await _unitOfWork.ReadingGoals.UpdateAsync(goal);
            }
        }

        // Single SaveChanges for all updates
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
