using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing reading goals and tracking progress.
/// </summary>
public interface IGoalService
{
    /// <summary>
    /// Event fired when goal progress may have changed (e.g., after a reading session).
    /// UI components should subscribe to refresh their goal displays.
    /// </summary>
    event EventHandler? GoalsChanged;

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

    // Book Exclusion
    Task<IReadOnlyList<GoalExcludedBook>> GetExcludedBooksAsync(Guid goalId, CancellationToken ct = default);
    Task ExcludeBookFromGoalAsync(Guid goalId, Guid bookId, CancellationToken ct = default);
    Task IncludeBookInGoalAsync(Guid goalId, Guid bookId, CancellationToken ct = default);

    // Genre Filter
    Task<IReadOnlyList<GoalGenre>> GetGoalGenresAsync(Guid goalId, CancellationToken ct = default);
    Task AddGenreToGoalAsync(Guid goalId, Guid genreId, CancellationToken ct = default);
    Task RemoveGenreFromGoalAsync(Guid goalId, Guid genreId, CancellationToken ct = default);

    /// <summary>
    /// Notifies subscribers that goal progress may have changed.
    /// Call this after completing a reading session or finishing a book.
    /// </summary>
    void NotifyGoalsChanged();
}
