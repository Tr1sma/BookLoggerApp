using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing books.
/// </summary>
public interface IBookService
{
    // Basic CRUD
    Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct = default);
    Task<Book?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Book> AddAsync(Book book, CancellationToken ct = default);
    Task UpdateAsync(Book book, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Advanced Queries
    Task<IReadOnlyList<Book>> GetByStatusAsync(ReadingStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Book>> GetByGenreAsync(Guid genreId, CancellationToken ct = default);
    Task<IReadOnlyList<Book>> SearchAsync(string query, CancellationToken ct = default);
    Task<Book?> GetByISBNAsync(string isbn, CancellationToken ct = default);

    // With Details (includes related data)
    Task<Book?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);

    // Bulk Operations
    Task<int> ImportBooksAsync(IEnumerable<Book> books, CancellationToken ct = default);

    // Statistics
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(ReadingStatus status, CancellationToken ct = default);

    // Status Updates
    Task StartReadingAsync(Guid bookId, CancellationToken ct = default);
    Task CompleteBookAsync(Guid bookId, CancellationToken ct = default);
    /// <summary>
    /// Updates the current page. Auto-completes and awards XP if the last page is reached.
    /// Returns the <see cref="ProgressionResult"/> when auto-completion occurs, <c>null</c> otherwise.
    /// </summary>
    Task<ProgressionResult?> UpdateProgressAsync(Guid bookId, int currentPage, CancellationToken ct = default);

    /// <summary>
    /// Atomically persists a book together with its genre/shelf/trope associations and
    /// wishlist cleanup in a single transaction (CODE_REVIEW BUG-16). Either everything
    /// commits or nothing does — no half-saved book without its relations. Completion
    /// side-effects (XP/goal-recalc via <see cref="CompleteBookAsync"/>) run AFTER the
    /// commit, so the status→Completed transition is the last step.
    /// </summary>
    /// <param name="book">The book to insert (Id unset/new) or update.</param>
    /// <param name="genreIds">Desired genre ids; the set is synced (add/remove diff).</param>
    /// <param name="shelfIds">Desired shelf ids; synced against <paramref name="manualShelfIds"/>.</param>
    /// <param name="tropeIds">Desired trope ids; synced (add/remove diff).</param>
    /// <param name="manualShelfIds">Shelf ids the editor is allowed to remove (manual,
    /// non-auto-sort shelves); auto-sort shelf memberships are never removed here.</param>
    Task<BookSaveResult> SaveBookWithRelationsAsync(
        Book book,
        IReadOnlyList<Guid> genreIds,
        IReadOnlyList<Guid> shelfIds,
        IReadOnlyList<Guid> tropeIds,
        IReadOnlyList<Guid> manualShelfIds,
        CancellationToken ct = default);
}
