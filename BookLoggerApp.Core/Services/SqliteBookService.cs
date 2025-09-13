using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using SQLite;

namespace BookLoggerApp.Core.Services;

public sealed class SqliteBookService : IBookService, IAsyncDisposable
{
    private readonly SQLiteAsyncConnection _db;

    public SqliteBookService(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        await _db.CreateTableAsync<Book>();
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Table<Book>().OrderBy(b => b.Title).ToListAsync();
    }

    public Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Table<Book>().Where(b => b.Id == id).FirstOrDefaultAsync();

    public async Task<Book> AddAsync(Book book, CancellationToken ct = default)
    {
        if (book.Id == Guid.Empty) book.Id = Guid.NewGuid();
        await _db.InsertAsync(book);
        return book;
    }

    public Task UpdateAsync(Book book, CancellationToken ct = default)
        => _db.UpdateAsync(book);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var b = await GetByIdAsync(id, ct);
        if (b is not null) await _db.DeleteAsync(b);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
