using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class BookEditViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;
    private readonly ILookupService _lookupService;
    private readonly IImageService _imageService;
    private readonly IShelfService _shelfService;
    private readonly IWishlistService _wishlistService;
    private readonly IShareCardService _shareCardService;
    private readonly IProgressService _progressService;

    /// <summary>
    /// Raised when a book share card PNG is ready. The component handles file write + sharing.
    /// </summary>
    public event Action<byte[]>? BookShareCardReady;

    public BookEditViewModel(
        IBookService bookService,
        IGenreService genreService,
        ILookupService lookupService,
        IImageService imageService,
        IShelfService shelfService,
        IWishlistService wishlistService,
        IShareCardService shareCardService,
        IProgressService progressService)
    {
        _bookService = bookService;
        _genreService = genreService;
        _lookupService = lookupService;
        _imageService = imageService;
        _shelfService = shelfService;
        _wishlistService = wishlistService;
        _shareCardService = shareCardService;
        _progressService = progressService;
    }

    [ObservableProperty]
    private Book? _book;

    [ObservableProperty]
    private List<Genre> _availableGenres = new();

    [ObservableProperty]
    private List<Guid> _selectedGenreIds = new();

    [ObservableProperty]
    private List<Shelf> _availableShelves = new();

    [ObservableProperty]
    private List<Guid> _selectedShelfIds = new();

    [ObservableProperty]
    private List<Trope> _availableTropes = new();

    [ObservableProperty]
    private List<Guid> _selectedTropeIds = new();

    [ObservableProperty]
    private bool _isLookingUpIsbn;

    [ObservableProperty]
    private string? _lookupMessage;

    /// <summary>
    /// Drives the lookup-message styling (error vs. success). Set alongside every
    /// <see cref="LookupMessage"/> assignment so the UI doesn't have to sniff the text.
    /// </summary>
    [ObservableProperty]
    private bool _lookupMessageIsError;

    [ObservableProperty]
    private bool _isSearchingTitle;

    /// <summary>
    /// Candidate books from the title search; non-empty shows the picker. A title search
    /// is inherently ambiguous (editions/similar titles), so the user picks the match.
    /// </summary>
    [ObservableProperty]
    private List<BookMetadata> _titleSearchResults = new();

    [ObservableProperty]
    private bool _showBookCompletionCelebration;

    [ObservableProperty]
    private bool _showDeleteConfirmation;

    [ObservableProperty]
    private bool _isDeleting;

    [ObservableProperty]
    private bool _bookDeleted;

    /// <summary>
    /// True only when an existing book transitions to Completed during SaveAsync.
    /// False for books added directly as Completed (no session data).
    /// </summary>
    [ObservableProperty]
    private bool _bookCompletedFromSession;

    [ObservableProperty]
    private bool _isGeneratingBookCard;

    [ObservableProperty]
    private ReadingStatus _selectedStatusForDisplay = ReadingStatus.Planned;

    // Track original status to detect when book becomes completed
    private ReadingStatus? _originalStatus;
    private bool _hasExplicitStatusChange;
    private bool _isInitializingStatusSelection;

    [RelayCommand]
    public async Task LoadAsync(Guid? bookId)
    {
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            AvailableGenres = (await _genreService.GetAllAsync(ct)).ToList();

            // Load and filter shelves (manual only for editing)
            var allShelves = await _shelfService.GetAllShelvesAsync();
            AvailableShelves = allShelves.Where(s => s.AutoSortRule == ShelfAutoSortRule.None).ToList();

            if (bookId.HasValue)
            {
                Book = await _bookService.GetWithDetailsAsync(bookId.Value, ct);
                if (Book != null)
                {
                    SelectedGenreIds = Book.BookGenres.Select(bg => bg.GenreId).ToList();
                    SelectedShelfIds = Book.BookShelves.Select(bs => bs.ShelfId).ToList();
                    SelectedTropeIds = Book.BookTropes.Select(bt => bt.TropeId).ToList();
                    await UpdateAvailableTropesAsync();
                    _originalStatus = Book.Status;
                    _hasExplicitStatusChange = false;

                    // Wishlist isn't a selectable status in the dropdown.
                    // Keep persisted status unchanged and map only the UI value.
                    _isInitializingStatusSelection = true;
                    SelectedStatusForDisplay = Book.Status == ReadingStatus.Wishlist
                        ? ReadingStatus.Planned
                        : Book.Status;
                    _isInitializingStatusSelection = false;
                }
            }
            else
            {
                // New book
                Book = new Book
                {
                    Id = Guid.Empty, // Explicitly set to Empty so navigation knows this is unsaved
                    Status = ReadingStatus.Planned,
                    DateAdded = DateTime.UtcNow
                };
                _originalStatus = null;
                _hasExplicitStatusChange = false;
                _isInitializingStatusSelection = true;
                SelectedStatusForDisplay = ReadingStatus.Planned;
                _isInitializingStatusSelection = false;
            }
        }, Tr("Error_FailedTo_LoadBook"));
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Book == null) return;

        // Reset celebration flags
        ShowBookCompletionCelebration = false;
        BookCompletedFromSession = false;

        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Book.Title) || string.IsNullOrWhiteSpace(Book.Author))
            {
                SetError(Tr("Error_BookTitleAuthorRequired"));
                return;
            }

            // New books need a stable Id BEFORE the atomic save so the cover file can be
            // named and child relations FK-wired (EF would otherwise generate it only on
            // insert, after the cover download). The save itself stays one transaction.
            if (Book.Id == Guid.Empty)
            {
                Book.Id = Guid.NewGuid();
            }

            // Cover download is network I/O and MUST stay outside the DB transaction.
            var coverImageUrl = Book.CoverImagePath;
            if (!string.IsNullOrWhiteSpace(coverImageUrl) &&
                (coverImageUrl.StartsWith("http://") || coverImageUrl.StartsWith("https://")))
            {
                var localPath = await _imageService.SaveCoverImageFromUrlAsync(coverImageUrl, Book.Id);
                if (localPath != null)
                {
                    Book.CoverImagePath = localPath;
                }
            }

            // Auto-select shelf if none selected (UI default; passed into the atomic save).
            if (SelectedShelfIds.Count == 0 && AvailableShelves.Any())
            {
                var mainShelf = AvailableShelves.FirstOrDefault(s => s.Name.Equals("Main Shelf", StringComparison.OrdinalIgnoreCase));
                if (mainShelf != null)
                {
                    SelectedShelfIds.Add(mainShelf.Id);
                }
                else
                {
                    var randomShelf = AvailableShelves.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                    if (randomShelf != null)
                    {
                        SelectedShelfIds.Add(randomShelf.Id);
                    }
                }
            }

            // Single transactional save: book record + genre/shelf/trope sync + wishlist
            // cleanup commit atomically; completion XP/goal-recalc run after commit
            // (CODE_REVIEW BUG-16). AvailableShelves = the manual shelves we may remove from.
            var manualShelfIds = AvailableShelves.Select(s => s.Id).ToList();
            var result = await _bookService.SaveBookWithRelationsAsync(
                Book, SelectedGenreIds, SelectedShelfIds, SelectedTropeIds, manualShelfIds);

            Book = result.Book;
            if (result.ShowCompletionCelebration)
            {
                ShowBookCompletionCelebration = true;
            }
            if (result.CompletedFromExisting)
            {
                BookCompletedFromSession = true;
            }

            _originalStatus = Book.Status;
        }, Tr("Error_FailedTo_SaveBook"));
    }

    [RelayCommand]
    public async Task LookupByIsbnAsync()
    {
        if (Book == null || string.IsNullOrWhiteSpace(Book.ISBN))
        {
            LookupMessage = Tr("Lookup_EnterIsbnFirst");
            LookupMessageIsError = true;
            return;
        }

        IsLookingUpIsbn = true;
        LookupMessage = null;

        try
        {
            var metadata = await _lookupService.LookupByISBNAsync(Book.ISBN);

            if (metadata == null)
            {
                LookupMessage = Tr("Lookup_NoBookFound");
                LookupMessageIsError = true;
                return;
            }

            await ApplyMetadataToBookAsync(metadata);

            LookupMessage = Tr("Lookup_LoadedSuccess");
            LookupMessageIsError = false;
        }
        catch (Exception ex)
        {
            ApplyLookupException(ex);
        }
        finally
        {
            IsLookingUpIsbn = false;
        }
    }

    /// <summary>
    /// Searches Google Books by the entered title (plus author, if any) and shows the
    /// candidate results for the user to pick from. Unlike ISBN lookup it does not fill
    /// the form directly, because a title query usually returns several matches.
    /// </summary>
    [RelayCommand]
    public async Task SearchByTitleAsync()
    {
        if (Book == null || string.IsNullOrWhiteSpace(Book.Title))
        {
            LookupMessage = Tr("Lookup_EnterTitleFirst");
            LookupMessageIsError = true;
            return;
        }

        IsSearchingTitle = true;
        LookupMessage = null;
        TitleSearchResults = new();

        try
        {
            var query = Book.Title.Trim();
            if (!string.IsNullOrWhiteSpace(Book.Author))
                query += " " + Book.Author.Trim();

            var results = await _lookupService.SearchBooksAsync(query);

            if (results == null || results.Count == 0)
            {
                LookupMessage = Tr("Lookup_NoResults");
                LookupMessageIsError = true;
                return;
            }

            TitleSearchResults = results.Take(5).ToList();
        }
        catch (Exception ex)
        {
            ApplyLookupException(ex);
        }
        finally
        {
            IsSearchingTitle = false;
        }
    }

    /// <summary>
    /// Applies a chosen title-search result to the form (same fill path as ISBN lookup)
    /// and dismisses the picker.
    /// </summary>
    [RelayCommand]
    public async Task SelectSearchResultAsync(BookMetadata metadata)
    {
        if (metadata == null || Book == null)
            return;

        await ApplyMetadataToBookAsync(metadata);

        TitleSearchResults = new();
        LookupMessage = Tr("Lookup_LoadedSuccess");
        LookupMessageIsError = false;
    }

    [RelayCommand]
    public void CloseTitleResults()
    {
        TitleSearchResults = new();
    }

    /// <summary>
    /// Maps fetched <see cref="BookMetadata"/> onto the current <see cref="Book"/>. Shared by
    /// ISBN lookup and title-search selection. Only non-empty fields overwrite existing values.
    /// </summary>
    private async Task ApplyMetadataToBookAsync(BookMetadata metadata)
    {
        if (Book == null)
            return;

        if (!string.IsNullOrWhiteSpace(metadata.Title))
            Book.Title = metadata.Title;

        if (!string.IsNullOrWhiteSpace(metadata.Author))
            Book.Author = metadata.Author;

        // A title-search result also carries the ISBN, so fill it in (harmless for the
        // ISBN-lookup path, where it equals the value the user already typed).
        if (!string.IsNullOrWhiteSpace(metadata.ISBN))
            Book.ISBN = metadata.ISBN;

        if (!string.IsNullOrWhiteSpace(metadata.Publisher))
            Book.Publisher = metadata.Publisher;

        if (metadata.PublicationYear.HasValue)
            Book.PublicationYear = metadata.PublicationYear;

        if (!string.IsNullOrWhiteSpace(metadata.Language))
            Book.Language = metadata.Language;

        if (!string.IsNullOrWhiteSpace(metadata.Description))
            Book.Description = metadata.Description;

        if (metadata.PageCount.HasValue)
            Book.PageCount = metadata.PageCount;

        // Handle cover image
        if (!string.IsNullOrWhiteSpace(metadata.CoverImageUrl))
        {
            // For new books (Id == Guid.Empty), we'll store the URL temporarily
            // and download it when the book is saved
            if (Book.Id == Guid.Empty)
            {
                // Store the URL temporarily for display
                Book.CoverImagePath = metadata.CoverImageUrl;
            }
            else
            {
                // For existing books, download and save the cover immediately
                var coverPath = await _imageService.SaveCoverImageFromUrlAsync(metadata.CoverImageUrl, Book.Id);
                if (coverPath != null)
                {
                    Book.CoverImagePath = coverPath;
                }
            }
        }

        // Handle genres/categories
        if (metadata.Categories != null && metadata.Categories.Count > 0)
        {
            await MapCategoriesToGenresAsync(metadata.Categories);
        }
    }

    /// <summary>
    /// Maps a lookup/search exception to a localized <see cref="LookupMessage"/>. Shared by
    /// ISBN lookup and title search so both report network/quota/timeout errors identically.
    /// </summary>
    private void ApplyLookupException(Exception ex)
    {
        LookupMessageIsError = true;
        switch (ex)
        {
            case HttpRequestException http when http.InnerException is System.Net.Sockets.SocketException:
                LookupMessage = Tr("Lookup_NoInternet");
                break;
            case HttpRequestException http when IsQuotaExceeded(http):
                LookupMessage = Tr("Lookup_QuotaReached");
                break;
            case HttpRequestException http:
                LookupMessage = http.StatusCode.HasValue
                    ? $"Lookup failed (HTTP {(int)http.StatusCode.Value}). Please try again."
                    : "Lookup failed. Please try again.";
                break;
            case TaskCanceledException:
                LookupMessage = Tr("Lookup_Timeout");
                break;
            default:
                LookupMessage = $"Error: {ex.Message}";
                break;
        }
    }

    private static bool IsQuotaExceeded(HttpRequestException ex)
    {
        if (ex.StatusCode == HttpStatusCode.TooManyRequests)
            return true;

        if (ex.StatusCode != HttpStatusCode.Forbidden)
            return false;

        return ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("rateLimitExceeded", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("dailyLimitExceeded", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase);
    }

    private async Task MapCategoriesToGenresAsync(List<string> categories)
    {
        // Try to match categories to existing genres
        var matchedGenreIds = new List<Guid>();

        foreach (var category in categories)
        {
            var matchingGenre = AvailableGenres.FirstOrDefault(g =>
                g.Name.Equals(category, StringComparison.OrdinalIgnoreCase) ||
                category.Contains(g.Name, StringComparison.OrdinalIgnoreCase) ||
                g.Name.Contains(category, StringComparison.OrdinalIgnoreCase));

            if (matchingGenre != null && !matchedGenreIds.Contains(matchingGenre.Id))
            {
                matchedGenreIds.Add(matchingGenre.Id);
            }
        }

        // Add matched genres to selected genres
        foreach (var genreId in matchedGenreIds)
        {
            if (!SelectedGenreIds.Contains(genreId))
            {
                SelectedGenreIds.Add(genreId);
            }
        }
    }

    [RelayCommand]
    public async Task GenerateAndShareBookCardAsync()
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            IsGeneratingBookCard = true;

            int totalMinutes = await _progressService.GetTotalMinutesAsync(Book.Id);

            byte[]? coverBytes = null;
            if (Book.Id != Guid.Empty)
            {
                var resized = await _imageService.GetResizedCoverImageAsync(Book.Id, 320, 480);
                coverBytes = resized?.Bytes;
            }

            var data = new BookShareData
            {
                Title = Book.Title,
                Author = Book.Author,
                PageCount = Book.PageCount,
                TotalMinutesRead = totalMinutes,
                AverageRating = Book.AverageRating,
                CoverImageBytes = coverBytes,
                CategoryRatings = new Dictionary<RatingCategory, int?>
                {
                    [RatingCategory.Characters]       = Book.CharactersRating,
                    [RatingCategory.Plot]             = Book.PlotRating,
                    [RatingCategory.WritingStyle]     = Book.WritingStyleRating,
                    [RatingCategory.SpiceLevel]       = Book.SpiceLevelRating,
                    [RatingCategory.Pacing]           = Book.PacingRating,
                    [RatingCategory.WorldBuilding]    = Book.WorldBuildingRating,
                    [RatingCategory.Spannung]         = Book.SpannungRating,
                    [RatingCategory.Humor]            = Book.HumorRating,
                    [RatingCategory.Informationsgehalt] = Book.InformationsgehaltRating,
                    [RatingCategory.EmotionaleTiefe]  = Book.EmotionaleTiefeRating,
                    [RatingCategory.Atmosphaere]      = Book.AtmosphaereRating
                }
            };

            byte[] cardBytes = await _shareCardService.GenerateBookCardAsync(data);
            BookShareCardReady?.Invoke(cardBytes);
        }, Tr("Error_FailedTo_GenerateBookShareCard"));

        IsGeneratingBookCard = false;
    }

    public Task OnBookCompletionCelebrationClose()
    {
        // Idempotent: a second invocation (rapid double-tap or back-button race) must not
        // re-run the state transition, otherwise downstream navigation/review-prompt logic
        // can fire twice.
        if (!ShowBookCompletionCelebration) return Task.CompletedTask;
        ShowBookCompletionCelebration = false;
        BookCompletedFromSession = false;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task DeleteBookAsync()
    {
        if (Book == null || Book.Id == Guid.Empty) return;

        IsDeleting = true;

        await ExecuteSafelyAsync(async () =>
        {
            await _bookService.DeleteAsync(Book.Id);
            BookDeleted = true;
            ShowDeleteConfirmation = false;
        }, "Buch konnte nicht gelöscht werden");

        IsDeleting = false;
    }

    partial void OnSelectedStatusForDisplayChanged(ReadingStatus value)
    {
        if (_isInitializingStatusSelection || Book == null)
        {
            return;
        }

        _hasExplicitStatusChange = true;
        Book.Status = value;
    }


    partial void OnSelectedGenreIdsChanged(List<Guid> value)
    {
        _ = ExecuteSafelyAsync(
            () => UpdateAvailableTropesAsync(),
            Tr("Error_FailedTo_UpdateAvailableTropes"));
    }

    public async Task ToggleGenreAsync(Guid genreId, bool isSelected)
    {
        if (isSelected && !SelectedGenreIds.Contains(genreId))
        {
            SelectedGenreIds.Add(genreId);
        }
        else if (!isSelected && SelectedGenreIds.Contains(genreId))
        {
            SelectedGenreIds.Remove(genreId);
        }

        // Manually trigger updates since we modified the list in-place
        await UpdateAvailableTropesAsync();
    }

    private async Task UpdateAvailableTropesAsync()
    {
        if (SelectedGenreIds == null || !SelectedGenreIds.Any())
        {
            AvailableTropes = new List<Trope>();
            return;
        }

        var allTropes = new List<Trope>();
        foreach (var genreId in SelectedGenreIds)
        {
            var tropes = await _genreService.GetTropesForGenreAsync(genreId);
            allTropes.AddRange(tropes);
        }
        
        // Use AvailableTropes setter to notify UI
        AvailableTropes = allTropes.DistinctBy(t => t.Id).OrderBy(t => t.Name).ToList();
    }
}
