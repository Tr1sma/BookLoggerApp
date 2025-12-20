using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Extensions;

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
            .Search(searchTerm)
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

    public async Task<Dictionary<string, int>> GetGenreStatsAsync()
    {
        var books = await _dbSet
            .AsNoTracking()
            .Include(b => b.BookGenres)
                .ThenInclude(bg => bg.Genre)
            .ToListAsync();

        return books
            .SelectMany(b => b.BookGenres.Select(bg => bg.Genre.Name))
            .GroupBy(name => name)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
