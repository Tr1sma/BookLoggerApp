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

    // Z.570 — eager-loading contract: GetSessionsByBookAsync includes Moods (book-detail timeline)
    // but not Book; GetSessionsInRangeAsync includes Book (stats) but deliberately not Moods — no
    // consumer reads them and this can span a year of sessions, so loading them is pure waste.
    public async Task<IEnumerable<ReadingSession>> GetSessionsByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        // Read-only (display/stats); don't pollute the change tracker (INK-10).
        return await _dbSet
            .AsNoTracking()
            .Include(rs => rs.Moods)
            .Where(rs => rs.BookId == bookId)
            .OrderByDescending(rs => rs.StartedAt)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ReadingSession>> GetSessionsInRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        // Read-only (stats over potentially a year of sessions); INK-10. Includes Book only — see
        // the eager-loading contract note above for why Moods are intentionally not loaded here.
        return await _dbSet
            .AsNoTracking()
            .Where(rs => rs.StartedAt >= startDate && rs.StartedAt <= endDate)
            .OrderBy(rs => rs.StartedAt)
            .Include(rs => rs.Book)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalMinutesReadAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(rs => rs.BookId == bookId)
            .SumAsync(rs => rs.Minutes, ct);
    }

    public async Task<int> GetTotalPagesReadAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _dbSet
            .Where(rs => rs.BookId == bookId && rs.PagesRead.HasValue)
            .SumAsync(rs => rs.PagesRead!.Value, ct);
    }

    public async Task<IEnumerable<ReadingSession>> GetRecentSessionsAsync(int count = 10, CancellationToken ct = default)
    {
        // Read-only (dashboard recent activity); INK-10.
        return await _dbSet
            .AsNoTracking()
            .OrderByDescending(rs => rs.StartedAt)
            .Take(count)
            .Include(rs => rs.Book)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalMinutesAsync(CancellationToken ct = default)
    {
        return await _dbSet.SumAsync(rs => rs.Minutes, ct);
    }
}
