using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing quotes.
/// </summary>
public class QuoteService : IQuoteService
{
    private readonly IUnitOfWork _unitOfWork;

    public QuoteService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Quote>> GetAllAsync(CancellationToken ct = default)
    {
        var quotes = await _unitOfWork.Quotes.GetAllAsync();
        return quotes.ToList();
    }

    public async Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.Quotes.GetByIdAsync(id);
    }

    public async Task<Quote> AddAsync(Quote quote, CancellationToken ct = default)
    {
        if (quote.CreatedAt == default)
            quote.CreatedAt = DateTime.UtcNow;

        var result = await _unitOfWork.Quotes.AddAsync(quote);
        await _unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task UpdateAsync(Quote quote, CancellationToken ct = default)
    {
        await _unitOfWork.Quotes.UpdateAsync(quote);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var quote = await _unitOfWork.Quotes.GetByIdAsync(id);
        if (quote != null)
        {
            await _unitOfWork.Quotes.DeleteAsync(quote);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesByBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var quotes = await _unitOfWork.Quotes.FindAsync(q => q.BookId == bookId);
        return quotes.ToList();
    }

    public async Task<IReadOnlyList<Quote>> GetFavoriteQuotesAsync(CancellationToken ct = default)
    {
        var quotes = await _unitOfWork.Quotes.FindAsync(q => q.IsFavorite);
        return quotes.ToList();
    }

    public async Task<IReadOnlyList<Quote>> SearchQuotesAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllAsync(ct);

        var lowerQuery = query.ToLower();
        var quotes = await _unitOfWork.Quotes.FindAsync(q => q.Text.ToLower().Contains(lowerQuery));
        return quotes.ToList();
    }

    public async Task ToggleFavoriteAsync(Guid quoteId, CancellationToken ct = default)
    {
        var quote = await _unitOfWork.Quotes.GetByIdAsync(quoteId);
        if (quote == null)
            throw new EntityNotFoundException(typeof(Quote), quoteId);

        quote.IsFavorite = !quote.IsFavorite;
        await _unitOfWork.Quotes.UpdateAsync(quote);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
