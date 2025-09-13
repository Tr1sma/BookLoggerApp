using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using SQLite;

namespace BookLoggerApp.Core.Services;

public sealed class SqliteProgressService : IProgressService
{
    private readonly SQLiteAsyncConnection _db;

    public SqliteProgressService(SQLiteAsyncConnection db)
    {
        _db = db;
    }

    public static async Task<SqliteProgressService> CreateAsync(string dbPath)
    {
        var conn = new SQLiteAsyncConnection(dbPath);
        await conn.CreateTableAsync<ReadingSession>();
        return new SqliteProgressService(conn);
    }

    public async Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        if (session.Id == Guid.Empty) session.Id = Guid.NewGuid();
        await _db.InsertAsync(session);
        return session;
    }

    public async Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _db.Table<ReadingSession>()
                        .Where(s => s.BookId == bookId)
                        .OrderByDescending(s => s.StartedAt)
                        .ToListAsync();
    }

    public async Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default)
    {
        var list = await _db.Table<ReadingSession>().Where(s => s.BookId == bookId).ToListAsync();
        return list.Sum(s => s.Minutes);
    }
}
