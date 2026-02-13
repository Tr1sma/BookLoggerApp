using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing the book wishlist.
/// </summary>
public interface IWishlistService
{
    Task<IReadOnlyList<Book>> GetWishlistBooksAsync(CancellationToken ct = default);
    Task<Book> AddToWishlistAsync(Book book, WishlistInfo? info = null, CancellationToken ct = default);
    Task<Book?> AddToWishlistByIsbnAsync(string isbn, CancellationToken ct = default);
    Task UpdateWishlistInfoAsync(Guid bookId, WishlistPriority priority, string? recommendedBy, string? notes, CancellationToken ct = default);
    Task MoveToLibraryAsync(Guid bookId, CancellationToken ct = default);
    Task RemoveFromWishlistAsync(Guid bookId, CancellationToken ct = default);
    Task<int> GetWishlistCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Book>> SearchWishlistAsync(string query, CancellationToken ct = default);
}
