using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository interface for Book entity with specific operations.
/// </summary>
public interface IBookRepository : IRepository<Book>
{
    Task<IEnumerable<Book>> GetBooksByStatusAsync(ReadingStatus status);
    Task<IEnumerable<Book>> GetBooksByGenreAsync(Guid genreId);
    Task<IEnumerable<Book>> SearchBooksAsync(string searchTerm);
    Task<Book?> GetBookWithDetailsAsync(Guid id);
    Task<IEnumerable<Book>> GetRecentBooksAsync(int count = 10);
    Task<IEnumerable<Book>> GetBooksByAuthorAsync(string author);
    Task<Book?> GetBookByISBNAsync(string isbn);
    Task<int> GetCountByCompletionYearAsync(int year, CancellationToken ct = default);
}
