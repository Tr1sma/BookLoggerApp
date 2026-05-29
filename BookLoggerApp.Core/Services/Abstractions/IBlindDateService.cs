using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for the "Blind Date with a Book" (Sub-TBR Roulette) feature. Provides the
/// pool of unread candidate books — eager-loaded with their tropes and genres — that
/// the UI presents as "wrapped" cards showing only the vibes (tropes) of each book.
/// </summary>
public interface IBlindDateService
{
    /// <summary>
    /// Returns the candidate books for a blind date: everything the user has not read
    /// yet (<see cref="ReadingStatus.Planned"/> TBR + <see cref="ReadingStatus.Wishlist"/>),
    /// with <c>BookTropes.Trope</c> and <c>BookGenres.Genre</c> included so the caller can
    /// build the vibe cards without further round-trips.
    /// </summary>
    Task<IReadOnlyList<Book>> GetCandidatesAsync(CancellationToken ct = default);
}
