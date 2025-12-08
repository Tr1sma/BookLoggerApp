using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository interface for ReadingSession entity with specific operations.
/// </summary>
public interface IReadingSessionRepository : IRepository<ReadingSession>
{
    Task<IEnumerable<ReadingSession>> GetSessionsByBookAsync(Guid bookId);
    Task<IEnumerable<ReadingSession>> GetSessionsInRangeAsync(DateTime startDate, DateTime endDate);
    Task<int> GetTotalMinutesReadAsync(Guid bookId);
    Task<int> GetTotalPagesReadAsync(Guid bookId);
    Task<IEnumerable<ReadingSession>> GetRecentSessionsAsync(int count = 10);
    Task<int> GetTotalMinutesAsync(CancellationToken ct = default);
    Task<int> GetTotalPagesReadInRangeAsync(DateTime startDate, DateTime endDate);
    Task<int> GetTotalMinutesReadInRangeAsync(DateTime startDate, DateTime endDate);
}
