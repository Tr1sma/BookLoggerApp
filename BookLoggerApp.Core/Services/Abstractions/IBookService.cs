using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing books.
/// </summary>
public interface IBookService
{
    // Basic CRUD
    Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default);
    Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Book> AddAsync(Book book, CancellationToken ct = default);
    Task UpdateAsync(Book book, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Advanced Queries
    Task<IReadOnlyList<Book>> GetByStatusAsync(ReadingStatus status, CancellationToken ct = default);
    Task<Book?> GetCurrentlyReadingBookAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Book>> GetByGenreAsync(Guid genreId, CancellationToken ct = default);
    Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken ct = default);
    Task<Book?> GetByISBNAsync(string isbn, CancellationToken ct = default);

    // With Details (includes related data)
    Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    // Bulk Operations
    Task<int> ImportBooksAsync(IEnumerable<Book> books, CancellationToken ct = default);

    // Statistics
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(ReadingStatus status, CancellationToken ct = default);
    Task<int> GetCompletedCountByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);

    // Status Updates
    Task StartReadingAsync(Guid bookId, CancellationToken ct = default);
    Task CompleteBookAsync(Guid bookId, CancellationToken ct = default);
    Task UpdateProgressAsync(Guid bookId, int currentPage, CancellationToken ct = default);
}
