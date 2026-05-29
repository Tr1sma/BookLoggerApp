namespace BookLoggerApp.Core.Models;

/// <summary>
/// A single "wrapped" card in the Blind Date roulette: the underlying <see cref="Book"/>
/// plus the 3–4 vibe tags shown to the user while the cover and title stay hidden.
/// Vibes are trope names; when a book has no tropes, the genre name(s) are used as a
/// fallback (flagged via <see cref="IsGenreFallback"/>). Trope and genre names are shown
/// verbatim — they are deliberately not localized.
/// </summary>
public class BlindDateCandidate
{
    public Book Book { get; init; } = null!;

    public IReadOnlyList<string> Vibes { get; init; } = new List<string>();

    /// <summary>
    /// True when <see cref="Vibes"/> were derived from the book's genre(s) (or a generic
    /// placeholder) because the book has no tropes assigned.
    /// </summary>
    public bool IsGenreFallback { get; init; }
}
