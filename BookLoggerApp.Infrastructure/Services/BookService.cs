using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

public class BookService : IBookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressionService _progressionService;
    private readonly IPlantService _plantService;
    private readonly IGoalService _goalService;
    private readonly ILogger<BookService> _logger;
    private readonly IAnalyticsService _analytics;

    public BookService(
        IUnitOfWork unitOfWork,
        IProgressionService progressionService,
        IPlantService plantService,
        IGoalService goalService,
        ILogger<BookService> logger,
        IAnalyticsService? analytics = null)
    {
        _unitOfWork = unitOfWork;
        _progressionService = progressionService;
        _plantService = plantService;
        _goalService = goalService;
        _logger = logger;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync();
        return books.OrderByDescending(b => b.DateAdded).ToList();
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.Books.GetByIdAsync(id);
    }

    public async Task<Book> AddAsync(Book book, CancellationToken ct = default)
    {
        if (book.DateAdded == default)
            book.DateAdded = DateTime.UtcNow;

        var result = await _unitOfWork.Books.AddAsync(book);
        await _unitOfWork.SaveChangesAsync(ct);

        _analytics.LogEvent(AnalyticsEventNames.BookAdded, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.HasCover, !string.IsNullOrEmpty(book.CoverImagePath))
            .Add(AnalyticsParamNames.PagesBucket, AnalyticsBuckets.Pages(book.PageCount ?? 0))
            .BuildMutable());

        return result;
    }

    public async Task UpdateAsync(Book book, CancellationToken ct = default)
    {
        try
        {
            await _unitOfWork.Books.UpdateAsync(book);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict updating book {BookId}", book.Id);
            throw new ConcurrencyException($"Book with ID {book.Id} was modified by another user. Please reload and try again.", ex);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _unitOfWork.Books.GetByIdAsync(id);
        if (book != null)
        {
            await _unitOfWork.Books.DeleteAsync(book);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Book>> GetByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetBooksByStatusAsync(status);
        return books.ToList();
    }

    public async Task<IReadOnlyList<Book>> GetByGenreAsync(Guid genreId, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetBooksByGenreAsync(genreId);
        return books.ToList();
    }

    public async Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllAsync(ct);
        }

        var books = await _unitOfWork.Books.SearchBooksAsync(query);
        return books.ToList();
    }

    public async Task<Book?> GetByISBNAsync(string isbn, CancellationToken ct = default)
    {
        return await _unitOfWork.Books.GetBookByISBNAsync(isbn);
    }

    public async Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.Books.GetBookWithDetailsAsync(id);
    }

    public async Task<int> ImportBooksAsync(IEnumerable<Book> books, CancellationToken ct = default)
    {
        var booksList = books.ToList();

        foreach (var book in booksList)
        {
            if (book.DateAdded == default)
                book.DateAdded = DateTime.UtcNow;
        }

        await _unitOfWork.Books.AddRangeAsync(booksList, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return booksList.Count;
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.Books.CountAsync();
    }

    public async Task<int> GetCountByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        return await _unitOfWork.Books.CountAsync(b => b.Status == status);
    }

    public async Task StartReadingAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await _unitOfWork.Books.GetByIdAsync(bookId);
        if (book == null)
            throw new EntityNotFoundException(typeof(Book), bookId);

        if (book.Status != ReadingStatus.Reading && book.Status != ReadingStatus.Completed)
        {
            book.Status = ReadingStatus.Reading;
        }

        book.DateStarted ??= DateTime.UtcNow;

        try
        {
            await _unitOfWork.Books.UpdateAsync(book);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict starting book {BookId}", bookId);
            throw new ConcurrencyException($"Book with ID {bookId} was modified by another user. Please reload and try again.", ex);
        }
    }

    public async Task CompleteBookAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await _unitOfWork.Books.GetByIdAsync(bookId);
        if (book == null)
            throw new EntityNotFoundException(typeof(Book), bookId);

        // Idempotency: XP/goal-recalc must fire once only.
        bool wasAlreadyCompleted = book.Status == ReadingStatus.Completed;

        book.Status = ReadingStatus.Completed;
        book.DateCompleted ??= DateTime.UtcNow;
        book.CurrentPage = book.PageCount ?? book.CurrentPage;

        try
        {
            await _unitOfWork.Books.UpdateAsync(book);
            await _unitOfWork.SaveChangesAsync(ct);

            if (!wasAlreadyCompleted)
            {
                var activePlant = await _plantService.GetActivePlantAsync(ct);
                await _progressionService.AwardBookCompletionXpAsync(activePlant?.Id, ct);

                await _goalService.RecalculateGoalProgressAsync(ct);
                _goalService.NotifyGoalsChanged();

                var avgRatingInt = book.AverageRating.HasValue ? (int?)Math.Round(book.AverageRating.Value) : null;
                _analytics.LogEvent(AnalyticsEventNames.BookCompleted, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.PagesBucket, AnalyticsBuckets.Pages(book.PageCount ?? 0))
                    .Add(AnalyticsParamNames.RatingBucket, AnalyticsBuckets.RatingInt(avgRatingInt))
                    .Add(AnalyticsParamNames.HasRating, avgRatingInt.HasValue)
                    .BuildMutable());
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict completing book {BookId}", bookId);
            throw new ConcurrencyException($"Book with ID {bookId} was modified by another user. Please reload and try again.", ex);
        }
    }

    public async Task<ProgressionResult?> UpdateProgressAsync(Guid bookId, int currentPage, CancellationToken ct = default)
    {
        var book = await _unitOfWork.Books.GetByIdAsync(bookId);
        if (book == null)
            throw new EntityNotFoundException(typeof(Book), bookId);

        // Idempotency: re-completing must not re-award XP or clobber DateCompleted.
        bool wasAlreadyCompleted = book.Status == ReadingStatus.Completed;
        // Clamp so UI can't persist >PageCount (display already clamps, raw value leaked).
        int clampedPage = Math.Max(0, currentPage);
        if (book.PageCount.HasValue)
        {
            clampedPage = Math.Min(clampedPage, book.PageCount.Value);
        }
        book.CurrentPage = clampedPage;
        bool justCompleted = false;

        if (book.PageCount.HasValue && currentPage >= book.PageCount.Value)
        {
            book.Status = ReadingStatus.Completed;
            book.DateCompleted ??= DateTime.UtcNow;
            justCompleted = !wasAlreadyCompleted;
        }

        try
        {
            await _unitOfWork.Books.UpdateAsync(book);
            await _unitOfWork.SaveChangesAsync(ct);

            ProgressionResult? completionResult = null;
            if (justCompleted)
            {
                var activePlant = await _plantService.GetActivePlantAsync(ct);
                completionResult = await _progressionService.AwardBookCompletionXpAsync(activePlant?.Id, ct);
            }

            await _goalService.RecalculateGoalProgressAsync(ct);
            _goalService.NotifyGoalsChanged();

            return completionResult;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict updating progress for book {BookId}", bookId);
            throw new ConcurrencyException($"Book with ID {bookId} was modified by another user. Please reload and try again.", ex);
        }
    }
}
