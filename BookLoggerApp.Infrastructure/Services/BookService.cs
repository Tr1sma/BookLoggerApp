using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>Manages books.</summary>
public class BookService : IBookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressionService _progressionService;
    private readonly IPlantService _plantService;
    private readonly IGoalService _goalService;
    private readonly ILogger<BookService> _logger;
    private readonly IAnalyticsService _analytics;
    private readonly IFeatureGuard? _featureGuard;
    private readonly IValidationService? _validation;

    public BookService(
        IUnitOfWork unitOfWork,
        IProgressionService progressionService,
        IPlantService plantService,
        IGoalService goalService,
        ILogger<BookService> logger,
        IAnalyticsService? analytics = null,
        IFeatureGuard? featureGuard = null,
        IValidationService? validation = null)
    {
        _unitOfWork = unitOfWork;
        _progressionService = progressionService;
        _plantService = plantService;
        _goalService = goalService;
        _logger = logger;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
        _featureGuard = featureGuard;
        _validation = validation;
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetAllAsync(ct);
        return books.OrderByDescending(b => b.DateAdded).ToList();
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.Books.GetByIdAsync(id, ct);
    }

    public async Task<Book> AddAsync(Book book, CancellationToken ct = default)
    {
        // CODE_REVIEW BUG-05: validate here so invalid data can't reach the DB via any caller.
        if (_validation is not null)
            await _validation.ValidateAndThrowAsync(book, ct);

        if (book.DateAdded == default)
            book.DateAdded = DateTime.UtcNow;

        var result = await _unitOfWork.Books.AddAsync(book, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _analytics.LogEvent(AnalyticsEventNames.BookAdded, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.HasCover, !string.IsNullOrEmpty(book.CoverImagePath))
            .Add(AnalyticsParamNames.PagesBucket, AnalyticsBuckets.Pages(book.PageCount ?? 0))
            .BuildMutable());

        return result;
    }

    public async Task UpdateAsync(Book book, CancellationToken ct = default)
    {
        // CODE_REVIEW BUG-05: intentionally NOT validating here. This is the in-place single-field
        // edit path (BookDetail) passing the whole entity; full validation would reject pre-existing
        // legacy violations the edit didn't introduce. Validation happens at AddAsync and
        // SaveBookWithRelationsAsync.
        try
        {
            await _unitOfWork.Books.UpdateAsync(book, ct);
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
        var book = await _unitOfWork.Books.GetByIdAsync(id, ct);
        if (book != null)
        {
            await _unitOfWork.Books.DeleteAsync(book, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Book>> GetByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetBooksByStatusAsync(status, ct);
        return books.ToList();
    }

    public async Task<IReadOnlyList<Book>> GetByGenreAsync(Guid genreId, CancellationToken ct = default)
    {
        var books = await _unitOfWork.Books.GetBooksByGenreAsync(genreId, ct);
        return books.ToList();
    }

    public async Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllAsync(ct);
        }

        var books = await _unitOfWork.Books.SearchBooksAsync(query, ct);
        return books.ToList();
    }

    public async Task<Book?> GetByISBNAsync(string isbn, CancellationToken ct = default)
    {
        return await _unitOfWork.Books.GetBookByISBNAsync(isbn, ct);
    }

    public async Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _unitOfWork.Books.GetBookWithDetailsAsync(id, ct);
    }

    public async Task<int> ImportBooksAsync(IEnumerable<Book> books, CancellationToken ct = default)
    {
        var booksList = books.ToList();

        // CODE_REVIEW BUG-05: validate every book up front so one invalid row aborts the whole
        // batch before any insert.
        if (_validation is not null)
        {
            foreach (var book in booksList)
                await _validation.ValidateAndThrowAsync(book, ct);
        }

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
        return await _unitOfWork.Books.CountAsync(ct);
    }

    public async Task<int> GetCountByStatusAsync(ReadingStatus status, CancellationToken ct = default)
    {
        return await _unitOfWork.Books.CountAsync(b => b.Status == status, ct);
    }

    public async Task StartReadingAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await _unitOfWork.Books.GetByIdAsync(bookId, ct);
        if (book == null)
            throw new EntityNotFoundException(typeof(Book), bookId);

        if (book.Status != ReadingStatus.Reading && book.Status != ReadingStatus.Completed)
        {
            book.Status = ReadingStatus.Reading;
        }

        book.DateStarted ??= DateTime.UtcNow;

        try
        {
            await _unitOfWork.Books.UpdateAsync(book, ct);
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
        var book = await _unitOfWork.Books.GetByIdAsync(bookId, ct);
        if (book == null)
            throw new EntityNotFoundException(typeof(Book), bookId);

        // Idempotency guard: callers may retry (double-tap, back-button race); completion-XP and
        // goal-recalc must fire exactly once.
        bool wasAlreadyCompleted = book.Status == ReadingStatus.Completed;

        book.Status = ReadingStatus.Completed;
        book.DateCompleted ??= DateTime.UtcNow;
        book.CurrentPage = book.PageCount ?? book.CurrentPage;

        try
        {
            await _unitOfWork.Books.UpdateAsync(book, ct);
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

    public async Task<BookSaveResult> SaveBookWithRelationsAsync(
        Book book,
        IReadOnlyList<Guid> genreIds,
        IReadOnlyList<Guid> shelfIds,
        IReadOnlyList<Guid> tropeIds,
        IReadOnlyList<Guid> manualShelfIds,
        CancellationToken ct = default)
    {
        // CODE_REVIEW BUG-05: load-bearing BookEdit save path. Validate up front so an invalid
        // book never opens the transaction or writes relation rows.
        if (_validation is not null)
            await _validation.ValidateAndThrowAsync(book, ct);

        // Derive new/existing + completion/wishlist decisions from PERSISTED state, not the edit.
        var persisted = book.Id == Guid.Empty
            ? null
            : await _unitOfWork.Books.GetByIdAsync(book.Id, ct);
        bool isNew = persisted == null;
        var persistedStatus = persisted?.Status;

        bool isBeingCompleted = book.Status == ReadingStatus.Completed
                                && persistedStatus.HasValue
                                && persistedStatus.Value != ReadingStatus.Completed;
        bool isLeavingWishlist = persistedStatus == ReadingStatus.Wishlist
                                 && book.Status != ReadingStatus.Wishlist;
        bool createdAsCompleted = isNew && book.Status == ReadingStatus.Completed;

        if (book.DateAdded == default)
            book.DateAdded = DateTime.UtcNow;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Book record
            if (isNew)
            {
                await _unitOfWork.Books.AddAsync(book, ct);
            }
            else
            {
                await _unitOfWork.Books.UpdateAsync(book, ct);

                if (isLeavingWishlist)
                {
                    var wishlistInfo = await _unitOfWork.Context.WishlistInfos
                        .FindAsync(new object[] { book.Id }, ct);
                    if (wishlistInfo != null)
                    {
                        _unitOfWork.Context.WishlistInfos.Remove(wishlistInfo);
                    }
                }
            }

            // Persist the book row first so child FKs resolve (esp. for a new book).
            await _unitOfWork.SaveChangesAsync(ct);

            // Genres (add/remove diff)
            var currentGenres = await _unitOfWork.Context.BookGenres
                .Where(bg => bg.BookId == book.Id)
                .ToListAsync(ct);
            var currentGenreIds = currentGenres.Select(bg => bg.GenreId).ToHashSet();

            foreach (var stale in currentGenres.Where(bg => !genreIds.Contains(bg.GenreId)))
            {
                _unitOfWork.Context.BookGenres.Remove(stale);
            }
            foreach (var genreId in genreIds.Where(id => !currentGenreIds.Contains(id)))
            {
                await _unitOfWork.Context.BookGenres.AddAsync(
                    new BookGenre { BookId = book.Id, GenreId = genreId, AddedAt = DateTime.UtcNow }, ct);
            }

            // Shelves (only manual shelves may be removed; new entries go to position 0)
            var currentShelfRows = await _unitOfWork.Context.BookShelves
                .Where(bs => bs.BookId == book.Id)
                .ToListAsync(ct);
            var currentShelfIds = currentShelfRows.Select(bs => bs.ShelfId).ToHashSet();

            foreach (var stale in currentShelfRows.Where(bs =>
                         !shelfIds.Contains(bs.ShelfId) && manualShelfIds.Contains(bs.ShelfId)))
            {
                _unitOfWork.Context.BookShelves.Remove(stale);
            }
            foreach (var shelfId in shelfIds.Where(id => !currentShelfIds.Contains(id)))
            {
                // Shift existing items forward so the new book lands at position 0,
                // matching ShelfService.AddBookToShelfAsync.
                var siblings = await _unitOfWork.Context.BookShelves
                    .Where(bs => bs.ShelfId == shelfId)
                    .ToListAsync(ct);
                foreach (var sibling in siblings)
                {
                    sibling.Position += 1;
                }
                _unitOfWork.Context.BookShelves.Add(
                    new BookShelf { ShelfId = shelfId, BookId = book.Id, Position = 0 });
            }

            // Tropes (add/remove diff)
            var currentTropes = await _unitOfWork.Context.BookTropes
                .Where(bt => bt.BookId == book.Id)
                .ToListAsync(ct);
            var currentTropeIds = currentTropes.Select(bt => bt.TropeId).ToHashSet();

            foreach (var stale in currentTropes.Where(bt => !tropeIds.Contains(bt.TropeId)))
            {
                _unitOfWork.Context.BookTropes.Remove(stale);
            }
            var newTropeIds = tropeIds.Where(id => !currentTropeIds.Contains(id)).ToList();
            if (newTropeIds.Count > 0)
            {
                // CODE_REVIEW SEC-17: load-bearing trope-tagging path. Adding NEW trope tags
                // requires Plus; removals above stay open so downgraded users can clean up.
                // The throw rolls back the whole transaction, so no partial save persists.
                _featureGuard?.RequireAccess(FeatureKey.Tropes, "Tropes require Plus.");
            }
            foreach (var tropeId in newTropeIds)
            {
                await _unitOfWork.Context.BookTropes.AddAsync(
                    new BookTrope { BookId = book.Id, TropeId = tropeId, AddedAt = DateTime.UtcNow }, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        // Completion side-effects run AFTER the commit: CompleteBookAsync is cross-service
        // (XP/goal-recalc) and idempotent.
        bool showCelebration = false;
        bool completedFromExisting = false;
        if (createdAsCompleted)
        {
            await CompleteBookAsync(book.Id, ct);
            showCelebration = true;
        }
        else if (isBeingCompleted)
        {
            await CompleteBookAsync(book.Id, ct);
            showCelebration = true;
            completedFromExisting = true;
        }

        return new BookSaveResult(book, showCelebration, completedFromExisting);
    }

    public async Task<ProgressionResult?> UpdateProgressAsync(Guid bookId, int currentPage, CancellationToken ct = default)
    {
        var book = await _unitOfWork.Books.GetByIdAsync(bookId, ct);
        if (book == null)
            throw new EntityNotFoundException(typeof(Book), bookId);

        // Idempotency guard: re-hitting the last page on an already-completed book must not
        // re-award XP nor clobber the original DateCompleted.
        bool wasAlreadyCompleted = book.Status == ReadingStatus.Completed;
        // Clamp to [0, PageCount] so a stray >PageCount UI input can't persist an inconsistent
        // "600 / 500 (100%)" state (raw CurrentPage used to leak through).
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
            await _unitOfWork.Books.UpdateAsync(book, ct);
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
