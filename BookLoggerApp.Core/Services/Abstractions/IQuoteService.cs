using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing quotes from books.
/// </summary>
public interface IQuoteService
{
    // Quote CRUD
    Task<IReadOnlyList<Quote>> GetAllAsync(CancellationToken ct = default);
    Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Quote> AddAsync(Quote quote, CancellationToken ct = default);
    Task UpdateAsync(Quote quote, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Query Quotes
    Task<IReadOnlyList<Quote>> GetQuotesByBookAsync(Guid bookId, CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> GetFavoriteQuotesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> SearchQuotesAsync(string query, CancellationToken ct = default);

    // Favorites
    Task ToggleFavoriteAsync(Guid quoteId, CancellationToken ct = default);
}
