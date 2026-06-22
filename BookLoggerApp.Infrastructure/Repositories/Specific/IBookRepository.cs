using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository interface for Book entity with specific operations.
/// </summary>
public interface IBookRepository : IRepository<Book>
{
    Task<IEnumerable<Book>> GetBooksByStatusAsync(ReadingStatus status, CancellationToken ct = default);
    Task<IEnumerable<Book>> GetBooksByGenreAsync(Guid genreId, CancellationToken ct = default);
    Task<IEnumerable<Book>> SearchBooksAsync(string searchTerm, CancellationToken ct = default);
    Task<Book?> GetBookWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Book>> GetRecentBooksAsync(int count = 10, CancellationToken ct = default);
    Task<IEnumerable<Book>> GetBooksByAuthorAsync(string author, CancellationToken ct = default);
    Task<Book?> GetBookByISBNAsync(string isbn, CancellationToken ct = default);
    Task<int> GetCountByCompletionYearAsync(int year, CancellationToken ct = default);
    Task<double> GetAverageRatingByCategoryAsync(RatingCategory category, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);
}
