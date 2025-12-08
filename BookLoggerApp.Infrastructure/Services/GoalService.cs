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

    /// <inheritdoc />
    public event EventHandler? GoalsChanged;

    /// <inheritdoc />
    public void NotifyGoalsChanged()
    {
        GoalsChanged?.Invoke(this, EventArgs.Empty);
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
        var goalsList = goals.ToList();

        // Calculate current progress for each goal dynamically
        await CalculateGoalProgressAsync(goalsList, ct);

        return goalsList;
    }

    public async Task<IReadOnlyList<ReadingGoal>> GetCompletedGoalsAsync(CancellationToken ct = default)
    {
        var goals = await _unitOfWork.ReadingGoals.GetCompletedGoalsAsync();
        var goalsList = goals.ToList();

        // Also calculate progress for completed goals to show final values
        await CalculateGoalProgressAsync(goalsList, ct);

        return goalsList;
    }

    private async Task CalculateGoalProgressAsync(List<ReadingGoal> goals, CancellationToken ct)
    {
        if (!goals.Any()) return;

        foreach (var goal in goals)
        {
            var startDate = goal.StartDate.Date;
            var endDate = goal.EndDate.Date.AddDays(1).AddTicks(-1); // End of day

            int current = goal.Current;

            switch (goal.Type)
            {
                case GoalType.Books:
                    current = await _unitOfWork.Books.GetCompletedBooksCountInRangeAsync(startDate, endDate);
                    break;
                case GoalType.Pages:
                    current = await _unitOfWork.ReadingSessions.GetTotalPagesReadInRangeAsync(startDate, endDate);
                    break;
                case GoalType.Minutes:
                    current = await _unitOfWork.ReadingSessions.GetTotalMinutesReadInRangeAsync(startDate, endDate);
                    break;
            }

            if (goal.Current != current)
            {
                goal.Current = current;
                // Auto-mark as completed if target is reached
                if (goal.Current >= goal.Target && !goal.IsCompleted)
                {
                    goal.IsCompleted = true;
                }
                await _unitOfWork.ReadingGoals.UpdateAsync(goal);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
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
