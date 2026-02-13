using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;

namespace BookLoggerApp.Infrastructure.Repositories;

/// <summary>
/// Unit of Work pattern for coordinating multiple repository operations
/// and managing transactions.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // ===== Specific Repositories =====

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

    // ===== Generic Repositories =====

    /// <summary>
    /// Repository for Genre entities.
    /// </summary>
    IRepository<Genre> Genres { get; }

    /// <summary>
    /// Repository for BookGenre junction entities.
    /// </summary>
    IRepository<BookGenre> BookGenres { get; }

    /// <summary>
    /// Repository for Quote entities.
    /// </summary>
    IRepository<Quote> Quotes { get; }

    /// <summary>
    /// Repository for Annotation entities.
    /// </summary>
    IRepository<Annotation> Annotations { get; }

    /// <summary>
    /// Repository for PlantSpecies entities.
    /// </summary>
    IRepository<PlantSpecies> PlantSpecies { get; }

    /// <summary>
    /// Repository for AppSettings entities.
    /// </summary>
    IRepository<AppSettings> AppSettingsRepo { get; }

    /// <summary>
    /// Repository for Trope entities.
    /// </summary>
    IRepository<Trope> Tropes { get; }

    /// <summary>
    /// Repository for BookTrope junction entities.
    /// </summary>
    IRepository<BookTrope> BookTropes { get; }

    /// <summary>
    /// Repository for WishlistInfo entities.
    /// </summary>
    IRepository<WishlistInfo> WishlistInfos { get; }

    /// <summary>
    /// Repository for GoalExcludedBook junction entities.
    /// </summary>
    IRepository<GoalExcludedBook> GoalExcludedBooks { get; }

    // ===== Direct Context Access =====

    /// <summary>
    /// Direct access to DbContext for complex queries (e.g., Include, Raw SQL).
    /// Use sparingly - prefer repository methods when possible.
    /// </summary>
    AppDbContext Context { get; }

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
