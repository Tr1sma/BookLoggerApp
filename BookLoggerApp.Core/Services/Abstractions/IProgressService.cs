using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for tracking reading progress and sessions.
/// </summary>
public interface IProgressService
{
    // Session Management
    Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default);
    Task<ReadingSession> StartSessionAsync(Guid bookId, CancellationToken ct = default);
    Task<SessionEndResult> EndSessionAsync(Guid sessionId, int pagesRead, CancellationToken ct = default);
    Task UpdateSessionAsync(ReadingSession session, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default);

    // Query Sessions
    Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingSession>> GetRecentSessionsAsync(int count = 10, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingSession>> GetSessionsInRangeAsync(DateTime start, DateTime end, CancellationToken ct = default);

    // Aggregations
    Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default);
    Task<int> GetTotalPagesAsync(Guid bookId, CancellationToken ct = default);
    Task<int> GetTotalMinutesAllBooksAsync(CancellationToken ct = default);

    // Statistics
    Task<Dictionary<DateTime, int>> GetMinutesByDateAsync(DateTime start, DateTime end, CancellationToken ct = default);
    Task<int> GetCurrentStreakAsync(CancellationToken ct = default);
}
