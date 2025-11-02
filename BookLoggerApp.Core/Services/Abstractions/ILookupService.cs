namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for looking up book metadata from external sources (e.g., ISBN lookup).
/// </summary>
public interface ILookupService
{
    /// <summary>
    /// Looks up book metadata by ISBN using Google Books API.
    /// </summary>
    /// <param name="isbn">The ISBN-10 or ISBN-13 to lookup.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Book metadata if found, null otherwise.</returns>
    Task<BookMetadata?> LookupByISBNAsync(string isbn, CancellationToken ct = default);

    /// <summary>
    /// Searches for books by title, author, or keywords.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching book metadata.</returns>
    Task<IReadOnlyList<BookMetadata>> SearchBooksAsync(string query, CancellationToken ct = default);
}

/// <summary>
/// Represents book metadata from external sources.
/// </summary>
public class BookMetadata
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public int? PageCount { get; set; }
    public string? Publisher { get; set; }
    public int? PublicationYear { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
    public string? Language { get; set; }
    public List<string> Categories { get; set; } = new();
}
