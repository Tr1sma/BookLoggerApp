using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing genres with caching support.
/// </summary>
public class GenreService : IGenreService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "AllGenres";

    public GenreService(IUnitOfWork unitOfWork, IMemoryCache cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Genre>> GetAllAsync(CancellationToken ct = default)
    {
        // Try to get cached genres
        if (_cache.TryGetValue(CacheKey, out List<Genre>? cached))
            return cached!;

        // Load from database if not cached
        var genres = await _unitOfWork.Genres.GetAllAsync(ct);
        var list = genres.ToList();

        // Cache for 24 hours (genres rarely change)
        _cache.Set(CacheKey, list, TimeSpan.FromHours(24));
        return list;
    }

    public async Task<Genre?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.Genres.GetByIdAsync(id);
    }

    public async Task<Genre> AddAsync(Genre genre, CancellationToken ct = default)
    {
        var result = await _unitOfWork.Genres.AddAsync(genre, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        // Invalidate cache when genres are modified
        _cache.Remove(CacheKey);
        return result;
    }

    public async Task UpdateAsync(Genre genre, CancellationToken ct = default)
    {
        await _unitOfWork.Genres.UpdateAsync(genre, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        // Invalidate cache when genres are modified
        _cache.Remove(CacheKey);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var genre = await _unitOfWork.Genres.GetByIdAsync(id, ct);
        if (genre != null)
        {
            await _unitOfWork.Genres.DeleteAsync(genre, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            // Invalidate cache when genres are modified
            _cache.Remove(CacheKey);
        }
    }

    public async Task AddGenreToBookAsync(Guid bookId, Guid genreId, CancellationToken ct = default)
    {
        var existing = await _unitOfWork.BookGenres.FindAsync(bg => bg.BookId == bookId && bg.GenreId == genreId);
        if (existing.Any())
            return; // Already exists

        var bookGenre = new BookGenre
        {
            BookId = bookId,
            GenreId = genreId,
            AddedAt = DateTime.UtcNow
        };

        await _unitOfWork.BookGenres.AddAsync(bookGenre);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveGenreFromBookAsync(Guid bookId, Guid genreId, CancellationToken ct = default)
    {
        var bookGenre = (await _unitOfWork.BookGenres.FindAsync(bg => bg.BookId == bookId && bg.GenreId == genreId)).FirstOrDefault();
        if (bookGenre != null)
        {
            await _unitOfWork.BookGenres.DeleteAsync(bookGenre);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Genre>> GetGenresForBookAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _unitOfWork.Context.BookGenres
            .Where(bg => bg.BookId == bookId)
            .Include(bg => bg.Genre)
            .Select(bg => bg.Genre)
            .ToListAsync(ct);
    }


    public async Task<IReadOnlyList<Trope>> GetTropesForGenreAsync(Guid genreId, CancellationToken ct = default)
    {
        return (await _unitOfWork.Tropes.FindAsync(t => t.GenreId == genreId)).ToList();
    }

    public async Task<IReadOnlyList<Trope>> GetTropesForBookAsync(Guid bookId, CancellationToken ct = default)
    {
        return await _unitOfWork.Context.BookTropes
            .Where(bt => bt.BookId == bookId)
            .Include(bt => bt.Trope)
            .Select(bt => bt.Trope)
            .ToListAsync(ct);
    }

    public async Task AddTropeToBookAsync(Guid bookId, Guid tropeId, CancellationToken ct = default)
    {
        var existing = await _unitOfWork.BookTropes.FindAsync(bt => bt.BookId == bookId && bt.TropeId == tropeId);
        if (existing.Any())
            return;

        var bookTrope = new BookTrope
        {
            BookId = bookId,
            TropeId = tropeId,
            AddedAt = DateTime.UtcNow
        };

        await _unitOfWork.BookTropes.AddAsync(bookTrope);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveTropeFromBookAsync(Guid bookId, Guid tropeId, CancellationToken ct = default)
    {
        var bookTrope = (await _unitOfWork.BookTropes.FindAsync(bt => bt.BookId == bookId && bt.TropeId == tropeId)).FirstOrDefault();
        if (bookTrope != null)
        {
            await _unitOfWork.BookTropes.DeleteAsync(bookTrope);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
