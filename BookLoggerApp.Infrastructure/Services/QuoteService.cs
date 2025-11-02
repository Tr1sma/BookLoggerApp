using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing quotes.
/// </summary>
public class QuoteService : IQuoteService
{
    private readonly IRepository<Quote> _quoteRepository;

    public QuoteService(IRepository<Quote> quoteRepository)
    {
        _quoteRepository = quoteRepository;
    }

    public async Task<IReadOnlyList<Quote>> GetAllAsync(CancellationToken ct = default)
    {
        var quotes = await _quoteRepository.GetAllAsync();
        return quotes.ToList();
    }

    public async Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _quoteRepository.GetByIdAsync(id);
    }

    public async Task<Quote> AddAsync(Quote quote, CancellationToken ct = default)
    {
        if (quote.CreatedAt == default)
            quote.CreatedAt = DateTime.UtcNow;

        return await _quoteRepository.AddAsync(quote);
    }

    public async Task UpdateAsync(Quote quote, CancellationToken ct = default)
    {
        await _quoteRepository.UpdateAsync(quote);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var quote = await _quoteRepository.GetByIdAsync(id);
        if (quote != null)
        {
            await _quoteRepository.DeleteAsync(quote);
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var quotes = await _quoteRepository.FindAsync(q => q.BookId == bookId);
        return quotes.ToList();
    }

    public async Task<IReadOnlyList<Quote>> GetFavoriteQuotesAsync(CancellationToken ct = default)
    {
        var quotes = await _quoteRepository.FindAsync(q => q.IsFavorite);
        return quotes.ToList();
    }

    public async Task<IReadOnlyList<Quote>> SearchQuotesAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync(ct);

        var lowerQuery = query.ToLower();
        var quotes = await _quoteRepository.FindAsync(q => q.Text.ToLower().Contains(lowerQuery));
        return quotes.ToList();
    }

    public async Task ToggleFavoriteAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await _quoteRepository.GetByIdAsync(quoteId);
        if (quote == null)
            throw new ArgumentException("Quote not found", nameof(quoteId));

        quote.IsFavorite = !quote.IsFavorite;
        await _quoteRepository.UpdateAsync(quote);
    }
}
