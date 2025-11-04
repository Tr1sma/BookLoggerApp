using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing genres.
/// </summary>
public class GenreService : IGenreService
{
    private readonly IRepository<Genre> _genreRepository;
    private readonly IRepository<BookGenre> _bookGenreRepository;
    private readonly AppDbContext _context;

    public GenreService(
        IRepository<Genre> genreRepository,
        IRepository<BookGenre> bookGenreRepository,
        AppDbContext context)
    {
        _genreRepository = genreRepository;
        _bookGenreRepository = bookGenreRepository;
        _context = context;
    }

    public async Task<IReadOnlyList<Genre>> GetAllAsync(CancellationToken ct = default)
    {
        var genres = await _genreRepository.GetAllAsync();
        return genres.ToList();
    }

    public async Task<Genre?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _genreRepository.GetByIdAsync(id);
    }

    public async Task<Genre> AddAsync(Genre genre, CancellationToken ct = default)
    {
        return await _genreRepository.AddAsync(genre);
    }

    public async Task UpdateAsync(Genre genre, CancellationToken ct = default)
    {
        await _genreRepository.UpdateAsync(genre);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var genre = await _genreRepository.GetByIdAsync(id);
        if (genre != null)
        {
            await _genreRepository.DeleteAsync(genre);
        }
    }

    public async Task AddGenreToBookAsync(Guid bookId, Guid genreId, CancellationToken ct = default)
    {
        var existing = await _bookGenreRepository.FindAsync(bg => bg.BookId == bookId && bg.GenreId == genreId);
        if (existing.Any())
            return; // Already exists

        var bookGenre = new BookGenre
        {
            BookId = bookId,
            GenreId = genreId,
            AddedAt = DateTime.UtcNow
        };

        await _bookGenreRepository.AddAsync(bookGenre);
    }

    public async Task RemoveGenreFromBookAsync(Guid bookId, Guid genreId, CancellationToken ct = default)
    {
        var bookGenre = (await _bookGenreRepository.FindAsync(bg => bg.BookId == bookId && bg.GenreId == genreId)).FirstOrDefault();
        if (bookGenre != null)
        {
            await _bookGenreRepository.DeleteAsync(bookGenre);
        }
    }

    public async Task<IReadOnlyList<Genre>> GetGenresForBookAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _context.BookGenres
            .Where(bg => bg.BookId == bookId)
            .Include(bg => bg.Genre)
            .Select(bg => bg.Genre)
            .ToListAsync(ct);
    }
}
