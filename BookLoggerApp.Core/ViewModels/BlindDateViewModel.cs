using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// VM for the "Blind Date with a Book" (Sub-TBR Roulette) feature. Loads the unread
/// book pool (TBR + Wishlist), presents a small random set of "wrapped" cards showing
/// only each book's vibes (tropes, or genre as fallback), and reveals the chosen book.
/// </summary>
public partial class BlindDateViewModel : ViewModelBase
{
    private const int CardsPerRound = 3;
    private const int MaxVibes = 4;

    private readonly IBlindDateService _blindDateService;

    private List<Book> _allCandidates = new();

    /// <summary>The "wrapped" cards currently offered to the user to pick from.</summary>
    public ObservableCollection<BlindDateCandidate> ShownCards { get; } = new();

    /// <summary>True once there is at least one eligible book in the pool.</summary>
    [ObservableProperty]
    private bool _hasCandidates;

    /// <summary>The book that was unwrapped, or <c>null</c> while still picking.</summary>
    [ObservableProperty]
    private Book? _revealedBook;

    [ObservableProperty]
    private bool _hasRevealed;

    public BlindDateViewModel(IBlindDateService blindDateService)
    {
        _blindDateService = blindDateService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            var books = await _blindDateService.GetCandidatesAsync();
            _allCandidates = books.ToList();
            HasCandidates = _allCandidates.Count > 0;
            HasRevealed = false;
            RevealedBook = null;
            DrawCards();
        }, Tr("Error_FailedTo_LoadBlindDate"));
    }

    /// <summary>Draws a fresh random set of wrapped cards from the pool.</summary>
    public void Shuffle()
    {
        HasRevealed = false;
        RevealedBook = null;
        DrawCards();
    }

    /// <summary>Unwraps the picked card and reveals its book.</summary>
    public void Reveal(BlindDateCandidate? card)
    {
        if (card is null)
        {
            return;
        }

        RevealedBook = card.Book;
        HasRevealed = true;
    }

    /// <summary>Returns from the reveal back to the current selection of cards.</summary>
    public void PickAnother()
    {
        HasRevealed = false;
        RevealedBook = null;
    }

    private void DrawCards()
    {
        ShownCards.Clear();
        foreach (var book in _allCandidates.OrderBy(_ => Random.Shared.Next()).Take(CardsPerRound))
        {
            ShownCards.Add(BuildCandidate(book));
        }
    }

    private BlindDateCandidate BuildCandidate(Book book)
    {
        var tropeNames = book.BookTropes
            .Select(bt => bt.Trope?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct()
            .ToList();

        if (tropeNames.Count > 0)
        {
            var vibes = tropeNames
                .OrderBy(_ => Random.Shared.Next())
                .Take(MaxVibes)
                .ToList();
            return new BlindDateCandidate { Book = book, Vibes = vibes, IsGenreFallback = false };
        }

        // Fallback: show the genre(s) when the book has no tropes.
        var genreNames = book.BookGenres
            .Select(bg => bg.Genre?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct()
            .Take(MaxVibes)
            .ToList();

        if (genreNames.Count == 0)
        {
            genreNames.Add(Tr("BlindDate_MysteryVibe"));
        }

        return new BlindDateCandidate { Book = book, Vibes = genreNames, IsGenreFallback = true };
    }
}
