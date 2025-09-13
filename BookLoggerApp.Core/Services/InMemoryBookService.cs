using System.Collections.Concurrent;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.Services;

/// <summary>
/// Simple in-memory store to unblock the UI in M0.
/// Not thread-safe for multi-process, but fine for local app.
/// </summary>
public sealed class InMemoryBookService : IBookService
{
    private readonly ConcurrentDictionary<Guid, Book> _books = new();

    public InMemoryBookService()
    {
        // Seed with a few demo entries
        var demo = new[]
        {
            new Book { Title = "Clean Code", Author = "Robert C. Martin", Status = ReadingStatus.Planned },
            new Book { Title = "The Pragmatic Programmer", Author = "Andrew Hunt / David Thomas", Status = ReadingStatus.Reading },
            new Book { Title = "Atomic Habits", Author = "James Clear", Status = ReadingStatus.Completed }
        };
        foreach (var b in demo) _books.TryAdd(b.Id, b);
    }

    public Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Book>>(_books.Values.OrderBy(b => b.Title).ToList());

    public Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _books.TryGetValue(id, out var book);
        return Task.FromResult(book);
    }

    public Task<Book> AddAsync(Book book, CancellationToken ct = default)
    {
        if (book.Id == Guid.Empty) book.Id = Guid.NewGuid();
        _books[book.Id] = book;
        return Task.FromResult(book);
    }

    public Task UpdateAsync(Book book, CancellationToken ct = default)
    {
        if (!_books.ContainsKey(book.Id))
            throw new KeyNotFoundException($"Book {book.Id} not found.");
        _books[book.Id] = book;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _books.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
