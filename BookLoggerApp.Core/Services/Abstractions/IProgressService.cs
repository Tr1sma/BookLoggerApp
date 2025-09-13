using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Tracks reading sessions and aggregates progress.
/// </summary>
public interface IProgressService
{
    Task<ReadingSession> AddSessionAsync(ReadingSession session, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default);
    Task<int> GetTotalMinutesAsync(Guid bookId, CancellationToken ct = default);
}
