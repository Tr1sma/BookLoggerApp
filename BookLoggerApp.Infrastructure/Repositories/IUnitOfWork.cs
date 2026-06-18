using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;

namespace BookLoggerApp.Infrastructure.Repositories;

public interface IUnitOfWork : IDisposable
{
    IBookRepository Books { get; }
    IReadingSessionRepository ReadingSessions { get; }
    IReadingGoalRepository ReadingGoals { get; }
    IUserPlantRepository UserPlants { get; }

    IRepository<Genre> Genres { get; }
    IRepository<BookGenre> BookGenres { get; }
    IRepository<Quote> Quotes { get; }
    IRepository<Annotation> Annotations { get; }
    IRepository<PlantSpecies> PlantSpecies { get; }
    IRepository<AppSettings> AppSettingsRepo { get; }
    IRepository<Trope> Tropes { get; }
    IRepository<BookTrope> BookTropes { get; }
    IRepository<WishlistInfo> WishlistInfos { get; }
    IRepository<GoalExcludedBook> GoalExcludedBooks { get; }
    IRepository<GoalGenre> GoalGenres { get; }

    /// <summary>Prefer repository methods when possible.</summary>
    AppDbContext Context { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
