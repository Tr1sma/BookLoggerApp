using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories.Specific;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing books.
/// </summary>
public class BookService : IBookService
{
    private readonly IBookRepository _bookRepository;

    public BookService(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
    {
        var books = await _bookRepository.GetAllAsync();
        return books.ToList();
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _bookRepository.GetByIdAsync(id);
    }

    public async Task<Book> AddAsync(Book book, CancellationToken ct = default)
    {
        // Business Logic: Set DateAdded if not set
        if (book.DateAdded == default)
            book.DateAdded = DateTime.UtcNow;

        return await _bookRepository.AddAsync(book);
    }

    public async Task UpdateAsync(Book book, CancellationToken ct = default)
    {
        await _bookRepository.UpdateAsync(book);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(id);
        if (book != null)
        {
            await _bookRepository.DeleteAsync(book);
        }
    }

    public async Task<IReadOnlyList<Book>> GetByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        var books = await _bookRepository.GetBooksByStatusAsync(status);
        return books.ToList();
    }

    public async Task<IReadOnlyList<Book>> GetByGenreAsync(Guid genreId, CancellationToken ct = default)
    {
        var books = await _bookRepository.GetBooksByGenreAsync(genreId);
        return books.ToList();
    }

    public async Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllAsync(ct);
        }

        var books = await _bookRepository.SearchBooksAsync(query);
        return books.ToList();
    }

    public async Task<Book?> GetByISBNAsync(string isbn, CancellationToken ct = default)
    {
        return await _bookRepository.GetBookByISBNAsync(isbn);
    }

    public async Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _bookRepository.GetBookWithDetailsAsync(id);
    }

    public async Task<int> ImportBooksAsync(IEnumerable<Book> books, CancellationToken ct = default)
    {
        var booksList = books.ToList();

        foreach (var book in booksList)
        {
            if (book.DateAdded == default)
                book.DateAdded = DateTime.UtcNow;

            await _bookRepository.AddAsync(book);
        }

        return booksList.Count;
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return await _bookRepository.CountAsync();
    }

    public async Task<int> GetCountByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        return await _bookRepository.CountAsync(b => b.Status == status);
    }

    public async Task StartReadingAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(bookId);
        if (book == null)
            throw new ArgumentException("Book not found", nameof(bookId));

        book.Status = ReadingStatus.Reading;
        book.DateStarted = DateTime.UtcNow;

        await _bookRepository.UpdateAsync(book);
    }

    public async Task CompleteBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(bookId);
        if (book == null)
            throw new ArgumentException("Book not found", nameof(bookId));

        book.Status = ReadingStatus.Completed;
        book.DateCompleted = DateTime.UtcNow;
        book.CurrentPage = book.PageCount ?? book.CurrentPage;

        await _bookRepository.UpdateAsync(book);
    }

    public async Task UpdateProgressAsync(Guid bookId, int currentPage, CancellationToken ct = default)
    {
        var book = await _bookRepository.GetByIdAsync(bookId);
        if (book == null)
            throw new ArgumentException("Book not found", nameof(bookId));

        book.CurrentPage = currentPage;

        // Auto-complete if reached last page
        if (book.PageCount.HasValue && currentPage >= book.PageCount.Value)
        {
            book.Status = ReadingStatus.Completed;
            book.DateCompleted = DateTime.UtcNow;
        }

        await _bookRepository.UpdateAsync(book);
    }
}
