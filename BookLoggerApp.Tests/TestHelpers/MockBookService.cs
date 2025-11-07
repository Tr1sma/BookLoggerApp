using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Mock implementation of IBookService for testing purposes.
/// Returns default/empty values for all operations.
/// </summary>
public class MockBookService : IBookService
{
    // Basic CRUD
    public Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Book>>(Array.Empty<Book>());
    }

    public Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult<Book?>(null);
    }

    public Task<Book> AddAsync(Book book, CancellationToken ct = default)
    {
        return Task.FromResult(book);
    }

    public Task UpdateAsync(Book book, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    // Advanced Queries
    public Task<IReadOnlyList<Book>> GetByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Book>>(Array.Empty<Book>());
    }

    public Task<IReadOnlyList<Book>> GetByGenreAsync(Guid genreId, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Book>>(Array.Empty<Book>());
    }

    public Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Book>>(Array.Empty<Book>());
    }

    public Task<Book?> GetByISBNAsync(string isbn, CancellationToken ct = default)
    {
        return Task.FromResult<Book?>(null);
    }

    // With Details (includes related data)
    public Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult<Book?>(null);
    }

    // Bulk Operations
    public Task<int> ImportBooksAsync(IEnumerable<Book> books, CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }

    // Statistics
    public Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }

    public Task<int> GetCountByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }

    // Status Updates
    public Task StartReadingAsync(Guid bookId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task CompleteBookAsync(Guid bookId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task UpdateProgressAsync(Guid bookId, int currentPage, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
