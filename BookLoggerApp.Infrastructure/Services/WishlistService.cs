using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing the book wishlist.
/// Uses DbContextFactory for thread-safe operations (same pattern as ShelfService).
/// </summary>
public class WishlistService : IWishlistService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILookupService _lookupService;

    public WishlistService(
        IDbContextFactory<AppDbContext> contextFactory,
        ILookupService lookupService)
    {
        _contextFactory = contextFactory;
        _lookupService = lookupService;
    }

    public async Task<IReadOnlyList<Book>> GetWishlistBooksAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Books
            .Include(b => b.WishlistInfo)
            .Where(b => b.Status == ReadingStatus.Wishlist)
            .OrderByDescending(b => b.WishlistInfo != null ? b.WishlistInfo.DateAddedToWishlist : b.DateAdded)
            .ToListAsync(ct);
    }

    public async Task<Book> AddToWishlistAsync(Book book, WishlistInfo? info = null, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        book.Status = ReadingStatus.Wishlist;
        book.DateAdded = DateTime.UtcNow;

        context.Books.Add(book);

        if (info != null)
        {
            info.BookId = book.Id;
            if (info.DateAddedToWishlist == default)
                info.DateAddedToWishlist = DateTime.UtcNow;
            context.WishlistInfos.Add(info);
        }
        else
        {
            context.WishlistInfos.Add(new WishlistInfo
            {
                BookId = book.Id,
                Priority = WishlistPriority.Medium,
                DateAddedToWishlist = DateTime.UtcNow
            });
        }

        // Single SaveChangesAsync ensures Book + WishlistInfo are saved atomically
        await context.SaveChangesAsync(ct);

        // Reload with WishlistInfo included
        var result = await context.Books
            .Include(b => b.WishlistInfo)
            .FirstOrDefaultAsync(b => b.Id == book.Id, ct);
        return result ?? throw new Core.Exceptions.EntityNotFoundException(typeof(Book), book.Id);
    }

    public async Task<Book?> AddToWishlistByIsbnAsync(string isbn, CancellationToken ct = default)
    {
        var metadata = await _lookupService.LookupByISBNAsync(isbn, ct);
        if (metadata == null)
            return null;

        var book = new Book
        {
            Title = metadata.Title,
            Author = metadata.Author,
            ISBN = metadata.ISBN,
            PageCount = metadata.PageCount,
            Publisher = metadata.Publisher,
            PublicationYear = metadata.PublicationYear,
            CoverImagePath = metadata.CoverImageUrl,
            Description = metadata.Description,
            Language = metadata.Language,
            Status = ReadingStatus.Wishlist
        };

        return await AddToWishlistAsync(book, null, ct);
    }

    public async Task UpdateWishlistInfoAsync(Guid bookId, WishlistPriority priority, string? recommendedBy, string? notes, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var info = await context.WishlistInfos.FindAsync(new object[] { bookId }, ct);

        if (info != null)
        {
            info.Priority = priority;
            info.RecommendedBy = recommendedBy;
            info.WishlistNotes = notes;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task MoveToLibraryAsync(Guid bookId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var book = await context.Books
            .Include(b => b.WishlistInfo)
            .FirstOrDefaultAsync(b => b.Id == bookId, ct);

        if (book == null) return;

        book.Status = ReadingStatus.Planned;
        book.CurrentPage = 0;
        book.DateAdded = DateTime.UtcNow;

        // Remove wishlist metadata
        if (book.WishlistInfo != null)
        {
            context.WishlistInfos.Remove(book.WishlistInfo);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveFromWishlistAsync(Guid bookId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var book = await context.Books
            .Include(b => b.WishlistInfo)
            .FirstOrDefaultAsync(b => b.Id == bookId, ct);

        if (book != null)
        {
            context.Books.Remove(book);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetWishlistCountAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Books.CountAsync(b => b.Status == ReadingStatus.Wishlist, ct);
    }

    public async Task ClearWishlistInfoAsync(Guid bookId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var info = await context.WishlistInfos.FindAsync(new object[] { bookId }, ct);
        if (info != null)
        {
            context.WishlistInfos.Remove(info);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Book>> SearchWishlistAsync(string query, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var lowerQuery = query.ToLowerInvariant();

        return await context.Books
            .Include(b => b.WishlistInfo)
            .Where(b => b.Status == ReadingStatus.Wishlist &&
                        (b.Title.ToLower().Contains(lowerQuery) ||
                         b.Author.ToLower().Contains(lowerQuery) ||
                         (b.ISBN != null && b.ISBN.Contains(query))))
            .OrderByDescending(b => b.WishlistInfo != null ? b.WishlistInfo.DateAddedToWishlist : b.DateAdded)
            .ToListAsync(ct);
    }
}
