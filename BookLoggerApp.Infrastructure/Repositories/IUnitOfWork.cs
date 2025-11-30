using BookLoggerApp.Infrastructure.Repositories.Specific;

namespace BookLoggerApp.Infrastructure.Repositories;

/// <summary>
/// Unit of Work pattern for coordinating multiple repository operations
/// and managing transactions.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Repository for Book entities.
    /// </summary>
    IBookRepository Books { get; }

    /// <summary>
    /// Repository for ReadingSession entities.
    /// </summary>
    IReadingSessionRepository ReadingSessions { get; }

    /// <summary>
    /// Repository for ReadingGoal entities.
    /// </summary>
    IReadingGoalRepository ReadingGoals { get; }

    /// <summary>
    /// Repository for UserPlant entities.
    /// </summary>
    IUserPlantRepository UserPlants { get; }

    /// <summary>
    /// Saves all changes made in this unit of work to the database.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task RollbackAsync(CancellationToken ct = default);
}
