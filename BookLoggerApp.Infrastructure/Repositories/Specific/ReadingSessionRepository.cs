using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository implementation for ReadingSession entity.
/// </summary>
public class ReadingSessionRepository : Repository<ReadingSession>, IReadingSessionRepository
{
    public ReadingSessionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ReadingSession>> GetSessionsByBookAsync(Guid bookId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(rs => rs.BookId == bookId)
            .OrderByDescending(rs => rs.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReadingSession>> GetSessionsInRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(rs => rs.StartedAt >= startDate && rs.StartedAt <= endDate)
            .OrderBy(rs => rs.StartedAt)
            .Include(rs => rs.Book)
            .ToListAsync();
    }

    public async Task<int> GetTotalMinutesReadAsync(Guid bookId)
    {
        return await _dbSet
            .Where(rs => rs.BookId == bookId)
            .SumAsync(rs => rs.Minutes);
    }

    public async Task<int> GetTotalPagesReadAsync(Guid bookId)
    {
        return await _dbSet
            .Where(rs => rs.BookId == bookId && rs.PagesRead.HasValue)
            .SumAsync(rs => rs.PagesRead!.Value);
    }

    public async Task<IEnumerable<ReadingSession>> GetRecentSessionsAsync(int count = 10)
    {
        return await _dbSet
            .AsNoTracking()
            .OrderByDescending(rs => rs.StartedAt)
            .Take(count)
            .Include(rs => rs.Book)
            .ToListAsync();
    }

    public async Task<int> GetTotalMinutesAsync(CancellationToken ct = default)
    {
        return await _dbSet.SumAsync(rs => rs.Minutes, ct);
    }
}
