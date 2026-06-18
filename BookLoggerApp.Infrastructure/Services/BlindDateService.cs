using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Services;

public class BlindDateService : IBlindDateService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public BlindDateService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Book>> GetCandidatesAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Books
            .Include(b => b.BookTropes)
                .ThenInclude(bt => bt.Trope)
            .Include(b => b.BookGenres)
                .ThenInclude(bg => bg.Genre)
            .Where(b => b.Status == ReadingStatus.Planned || b.Status == ReadingStatus.Wishlist)
            .ToListAsync(ct);
    }
}
