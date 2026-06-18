using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for managing books.
/// </summary>
public class BookService : IBookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProgressionService _progressionService;
    private readonly IPlantService _plantService;
    private readonly IGoalService _goalService;
    private readonly ILogger<BookService> _logger;
    private readonly IAnalyticsService _analytics;
    private readonly IFeatureGuard? _featureGuard;
    private readonly IValidationService? _validationService;

    public BookService(
        IUnitOfWork unitOfWork,
        IProgressionService progressionService,
        IPlantService plantService,
        IGoalService goalService,
        ILogger<BookService> logger,
        IAnalyticsService? analytics = null,
        IFeatureGuard? featureGuard = null,
        IValidationService? validationService = null)
    {
        _unitOfWork = unitOfWork;
        _progressionService = progressionService;
        _plantService = plantService;
        _goalService = goalService;
        _logger = logger;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
        _featureGuard = featureGuard;
        _validationService = validationService;
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
        // CODE_REVIEW BUG-05: enforce the FluentValidation rules on the write path
        // (empty/over-length Title/Author, negative/absurd pages, future/inconsistent dates)
        // before any persistence so garbage data can never reach SQLite.
        if (_validationService is not null)
            await _validationService.ValidateAndThrowAsync(book, ct);

        // Business Logic: Set DateAdded if not set
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
        // CODE_REVIEW BUG-05 (review follow-up): intentionally NOT validating here. UpdateAsync is the
        // in-place single-field edit path (BookDetail rating/notes) which passes the WHOLE entity; running
        // the full BookValidator would reject pre-existing legacy violations (e.g. CurrentPage > PageCount
        // from before the progress clamp) that the edit did not introduce, silently dropping the change.
        // User-entered data is validated at the true entry points: AddAsync and SaveBookWithRelationsAsync.
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

        // Business Logic: Set DateAdded for books where not set
        foreach (var book in booksList)
        {
            if (book.DateAdded == default)
                book.DateAdded = DateTime.UtcNow;
        }

        // Bulk insert for better performance
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

        // Idempotency guard: callers may retry (rapid double-tap, VM back-button race).
        // Completion-XP and goal-recalc must fire exactly once across repeated calls.
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
        // CODE_REVIEW BUG-05: this is the load-bearing BookEdit save path; validate the book
        // up front (before the transaction) so an invalid edit fails fast with nothing persisted.
        if (_validationService is not null)
            await _validationService.ValidateAndThrowAsync(book, ct);

        // Derive the new/existing + completion/wishlist decisions from PERSISTED state
        // before touching anything, so they reflect the DB, not the in-memory edit.
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
            // ---- Book record ----
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

            // Persist the book row first so child FKs resolve (esp. for a brand-new book).
            await _unitOfWork.SaveChangesAsync(ct);

            // ---- Genres (add/remove diff) ----
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

            // ---- Shelves (only manual shelves may be removed; new entries go to position 0) ----
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
                // Shift existing items forward so the new book lands first (position 0),
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

            // ---- Tropes (add/remove diff) ----
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
                // CODE_REVIEW SEC-17: this is the load-bearing trope-tagging path (BookEdit saves
                // via SaveBookWithRelationsAsync, not GenreService.AddTropeToBookAsync). Adding NEW
                // trope tags requires Plus; removals above stay open so downgraded users can clean
                // up. The throw rolls back the whole transaction, so no partial save persists.
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

        // Completion side-effects run AFTER the commit ("Status→Completed zuletzt"):
        // CompleteBookAsync is cross-service (XP/goal-recalc) and idempotent.
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

        // Idempotency guard: if the book is already completed, hitting the last page again
        // (e.g. after the user scrubbed the page slider down and back up) must not re-award
        // completion XP nor clobber the original DateCompleted.
        bool wasAlreadyCompleted = book.Status == ReadingStatus.Completed;
        // Clamp to [0, PageCount] so a stray >PageCount input from the UI cannot persist
        // an inconsistent "600 / 500 (100%)" state — the percentage is already clamped
        // for display but the raw CurrentPage used to leak through.
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
