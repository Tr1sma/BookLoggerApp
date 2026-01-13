using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

/// <summary>
/// Repository implementation for Book entity.
/// </summary>
public class BookRepository : Repository<Book>, IBookRepository
{
    public BookRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Book>> GetBooksByStatusAsync(ReadingStatus status)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.DateAdded)
            .ToListAsync();
    }

    public async Task<IEnumerable<Book>> GetBooksByGenreAsync(Guid genreId)
    {
        return await _dbSet
            .Where(b => b.BookGenres.Any(bg => bg.GenreId == genreId))
            .Include(b => b.BookGenres)
                .ThenInclude(bg => bg.Genre)
            .ToListAsync();
    }

    public async Task<IEnumerable<Book>> SearchBooksAsync(string searchTerm)
    {
        return await _dbSet
            .Where(b => EF.Functions.Like(b.Title, $"%{searchTerm}%") ||
                       EF.Functions.Like(b.Author, $"%{searchTerm}%") ||
                       (b.ISBN != null && EF.Functions.Like(b.ISBN, $"%{searchTerm}%")))
            .Include(b => b.BookGenres)
                .ThenInclude(bg => bg.Genre)
            .ToListAsync();
    }

    public async Task<Book?> GetBookWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(b => b.BookGenres)
                .ThenInclude(bg => bg.Genre)
            .Include(b => b.BookTropes)
                .ThenInclude(bt => bt.Trope)
            .Include(b => b.ReadingSessions)
            .Include(b => b.Quotes)
            .Include(b => b.Annotations)
            .Include(b => b.BookShelves)
                .ThenInclude(bs => bs.Shelf)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IEnumerable<Book>> GetRecentBooksAsync(int count = 10)
    {
        return await _dbSet
            .OrderByDescending(b => b.DateAdded)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Book>> GetBooksByAuthorAsync(string author)
    {
        return await _dbSet
            .Where(b => EF.Functions.Like(b.Author, author))
            .OrderByDescending(b => b.DateAdded)
            .ToListAsync();
    }

    public async Task<Book?> GetBookByISBNAsync(string isbn)
    {
        return await _dbSet
            .FirstOrDefaultAsync(b => b.ISBN == isbn);
    }

    public async Task<int> GetCountByCompletionYearAsync(int year, CancellationToken ct = default)
    {
        var startOfYear = new DateTime(year, 1, 1);
        var startOfNextYear = startOfYear.AddYears(1);

        return await _dbSet
            .AsNoTracking()
            .Where(b => b.Status == ReadingStatus.Completed
                        && b.DateCompleted.HasValue
                        && b.DateCompleted.Value >= startOfYear
                        && b.DateCompleted.Value < startOfNextYear)
            .CountAsync(ct);
    }

    public async Task<double> GetAverageRatingByCategoryAsync(RatingCategory category, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking().Where(b => b.Status == ReadingStatus.Completed);

        if (startDate.HasValue)
            query = query.Where(b => b.DateCompleted.HasValue && b.DateCompleted.Value >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(b => b.DateCompleted.HasValue && b.DateCompleted.Value <= endDate.Value);

        IQueryable<int?>? ratingQuery = category switch
        {
            RatingCategory.Characters => query.Select(b => b.CharactersRating),
            RatingCategory.Plot => query.Select(b => b.PlotRating),
            RatingCategory.WritingStyle => query.Select(b => b.WritingStyleRating),
            RatingCategory.SpiceLevel => query.Select(b => b.SpiceLevelRating),
            RatingCategory.Pacing => query.Select(b => b.PacingRating),
            RatingCategory.WorldBuilding => query.Select(b => b.WorldBuildingRating),
            _ => null
        };

        if (ratingQuery == null) return 0;

        return await ratingQuery
            .Where(r => r.HasValue)
            .AverageAsync(r => (double?)r, ct) ?? 0;
    }
}
