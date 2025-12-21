using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Mock implementation of IBookService for testing purposes.
/// Maintains an in-memory collection of books for testing.
/// </summary>
public class MockBookService : IBookService
{
    private readonly Dictionary<Guid, Book> _books = new();

    // Basic CRUD
    public Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Book>>(_books.Values.ToList());
    }

    public Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _books.TryGetValue(id, out var book);
        return Task.FromResult(book);
    }

    public Task<Book> AddAsync(Book book, CancellationToken ct = default)
    {
        if (book.Id == Guid.Empty)
            book.Id = Guid.NewGuid();

        _books[book.Id] = book;
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

    public Task<Book?> GetCurrentlyReadingBookAsync(CancellationToken ct = default)
    {
        return Task.FromResult<Book?>(null);
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

    public Task<int> GetCompletedCountByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
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
