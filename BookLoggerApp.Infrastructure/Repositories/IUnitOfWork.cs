using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;

namespace BookLoggerApp.Infrastructure.Repositories;

/// <summary>
/// Unit of Work coordinating multiple repositories and managing transactions.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>Repository for Book entities.</summary>
    IBookRepository Books { get; }

    /// <summary>Repository for ReadingSession entities.</summary>
    IReadingSessionRepository ReadingSessions { get; }

    /// <summary>Repository for ReadingGoal entities.</summary>
    IReadingGoalRepository ReadingGoals { get; }

    /// <summary>Repository for UserPlant entities.</summary>
    IUserPlantRepository UserPlants { get; }

    /// <summary>Repository for Genre entities.</summary>
    IRepository<Genre> Genres { get; }

    /// <summary>Repository for BookGenre junction entities.</summary>
    IRepository<BookGenre> BookGenres { get; }

    /// <summary>Repository for Quote entities.</summary>
    IRepository<Quote> Quotes { get; }

    /// <summary>Repository for Annotation entities.</summary>
    IRepository<Annotation> Annotations { get; }

    /// <summary>Repository for PlantSpecies entities.</summary>
    IRepository<PlantSpecies> PlantSpecies { get; }

    /// <summary>Repository for AppSettings entities.</summary>
    IRepository<AppSettings> AppSettingsRepo { get; }

    /// <summary>Repository for Trope entities.</summary>
    IRepository<Trope> Tropes { get; }

    /// <summary>Repository for BookTrope junction entities.</summary>
    IRepository<BookTrope> BookTropes { get; }

    /// <summary>Repository for WishlistInfo entities.</summary>
    IRepository<WishlistInfo> WishlistInfos { get; }

    /// <summary>Repository for GoalExcludedBook junction entities.</summary>
    IRepository<GoalExcludedBook> GoalExcludedBooks { get; }

    /// <summary>Repository for GoalGenre junction entities.</summary>
    IRepository<GoalGenre> GoalGenres { get; }

    /// <summary>
    /// Direct DbContext access for complex queries (Include, raw SQL). Use sparingly.
    /// </summary>
    AppDbContext Context { get; }

    /// <summary>Saves all changes in this unit of work.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Begins a new database transaction.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>Commits the current transaction.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rolls back the current transaction.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task RollbackAsync(CancellationToken ct = default);
}
