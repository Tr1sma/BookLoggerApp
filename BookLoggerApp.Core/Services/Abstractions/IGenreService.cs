using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing genres and book-genre associations.
/// </summary>
public interface IGenreService
{
    // Genre CRUD
    Task<IReadOnlyList<Genre>> GetAllAsync(CancellationToken ct = default);
    Task<Genre?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Genre> AddAsync(Genre genre, CancellationToken ct = default);
    Task UpdateAsync(Genre genre, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Book-Genre Association
    Task AddGenreToBookAsync(Guid bookId, Guid genreId, CancellationToken ct = default);
    Task RemoveGenreFromBookAsync(Guid bookId, Guid genreId, CancellationToken ct = default);
    Task<IReadOnlyList<Genre>> GetGenresForBookAsync(Guid bookId, CancellationToken ct = default);
}
