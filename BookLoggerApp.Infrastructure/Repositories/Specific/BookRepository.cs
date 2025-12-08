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
            .Include(b => b.ReadingSessions)
            .Include(b => b.Quotes)
            .Include(b => b.Annotations)
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

    public async Task<int> GetCompletedBooksCountInRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .CountAsync(b => b.Status == ReadingStatus.Completed &&
                             b.DateCompleted.HasValue &&
                             b.DateCompleted.Value >= startDate &&
                             b.DateCompleted.Value <= endDate);
    }

    public async Task<Dictionary<string, int>> GetGenreDistributionAsync()
    {
        return await _context.Set<BookGenre>()
            .Include(bg => bg.Genre)
            .GroupBy(bg => bg.Genre.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Count);
    }

    public async Task<Dictionary<RatingCategory, double>> GetAverageRatingsProfileAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbSet.AsNoTracking().Where(b => b.Status == ReadingStatus.Completed);

        if (startDate.HasValue)
            query = query.Where(b => b.DateCompleted.HasValue && b.DateCompleted.Value >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(b => b.DateCompleted.HasValue && b.DateCompleted.Value <= endDate.Value);

        var stats = await query
            .GroupBy(x => 1)
            .Select(g => new
            {
                Characters = g.Average(b => (double?)b.CharactersRating),
                Plot = g.Average(b => (double?)b.PlotRating),
                WritingStyle = g.Average(b => (double?)b.WritingStyleRating),
                SpiceLevel = g.Average(b => (double?)b.SpiceLevelRating),
                Pacing = g.Average(b => (double?)b.PacingRating),
                WorldBuilding = g.Average(b => (double?)b.WorldBuildingRating),
                Overall = g.Average(b => (double?)b.OverallRating)
            })
            .FirstOrDefaultAsync();

        var result = new Dictionary<RatingCategory, double>();

        if (stats != null)
        {
            result[RatingCategory.Characters] = stats.Characters ?? 0;
            result[RatingCategory.Plot] = stats.Plot ?? 0;
            result[RatingCategory.WritingStyle] = stats.WritingStyle ?? 0;
            result[RatingCategory.SpiceLevel] = stats.SpiceLevel ?? 0;
            result[RatingCategory.Pacing] = stats.Pacing ?? 0;
            result[RatingCategory.WorldBuilding] = stats.WorldBuilding ?? 0;
            result[RatingCategory.Overall] = stats.Overall ?? 0;
        }
        else
        {
            foreach (RatingCategory category in Enum.GetValues(typeof(RatingCategory)))
            {
                result[category] = 0;
            }
        }

        return result;
    }

    public async Task<double> GetAverageOverallRatingAsync()
    {
        return await _dbSet
            .Where(b => b.OverallRating.HasValue)
            .AverageAsync(b => (double?)b.OverallRating) ?? 0;
    }

    public async Task<IEnumerable<Book>> GetBooksWithGenresAsync()
    {
        return await _dbSet
            .Include(b => b.BookGenres)
                .ThenInclude(bg => bg.Genre)
            .ToListAsync();
    }
}
