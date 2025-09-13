using System.Collections.Concurrent;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.Services;

/// <summary>
/// In-memory reading session tracker for M0.
/// </summary>
public sealed class InMemoryProgressService : IProgressService
{
    // Key: BookId
    private readonly ConcurrentDictionary<Guid, List<ReadingSession>> _sessions = new();

    public Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default)
    {
        var list = _sessions.GetOrAdd(session.BookId, _ => new List<ReadingSession>());
        list.Add(session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var list = _sessions.TryGetValue(bookId, out var found) ? found : new List<ReadingSession>();
        // Return copy to avoid external mutation
        return Task.FromResult<IReadOnlyList<ReadingSession>>(list.OrderByDescending(s => s.StartedAt).ToList());
    }

    public Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default)
    {
        var total = _sessions.TryGetValue(bookId, out var found) ? found.Sum(s => s.Minutes) : 0;
        return Task.FromResult(total);
    }
}
