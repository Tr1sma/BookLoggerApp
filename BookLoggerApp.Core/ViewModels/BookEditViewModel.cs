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
        await ExecuteSafelyWithDbAsync(async () =>
        {
            AvailableGenres = (await _genreService.GetAllAsync()).ToList();

            // Load and filter shelves (manual only for editing)
            var allShelves = await _shelfService.GetAllShelvesAsync();
            AvailableShelves = allShelves.Where(s => s.AutoSortRule == ShelfAutoSortRule.None).ToList();

            if (bookId.HasValue)
            {
                Book = await _bookService.GetWithDetailsAsync(bookId.Value);
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

            var persistedBook = Book.Id == Guid.Empty
                ? null
                : await _bookService.GetByIdAsync(Book.Id);
            var isNewBook = Book.Id == Guid.Empty || persistedBook == null;
            var coverImageUrl = Book.CoverImagePath;

            // Check if book is being marked as completed for the first time based on persisted DB state
            var persistedStatus = persistedBook?.Status;
            var isBeingCompleted = Book.Status == ReadingStatus.Completed &&
                                   persistedStatus.HasValue &&
                                   persistedStatus.Value != ReadingStatus.Completed;

            // Check if book is leaving wishlist status based on persisted DB state
            var isLeavingWishlist = persistedStatus == ReadingStatus.Wishlist &&
                                    Book.Status != ReadingStatus.Wishlist;

            // Phase 1: persist the book record itself (without triggering goal-recalc yet).
            // For new books: optionally set "create as completed" flag to handle after genres.
            bool createdAsCompleted = false;
            if (isNewBook)
            {
                Book = await _bookService.AddAsync(Book);

                // Download and save cover image if it's a URL
                if (!string.IsNullOrWhiteSpace(coverImageUrl) &&
                    (coverImageUrl.StartsWith("http://") || coverImageUrl.StartsWith("https://")))
                {
                    var localPath = await _imageService.SaveCoverImageFromUrlAsync(coverImageUrl, Book.Id);
                    if (localPath != null)
                    {
                        Book.CoverImagePath = localPath;
                        await _bookService.UpdateAsync(Book);
                    }
                }

                createdAsCompleted = Book.Status == ReadingStatus.Completed;
            }
            else
            {
                await _bookService.UpdateAsync(Book);

                if (isLeavingWishlist)
                {
                    await _wishlistService.ClearWishlistInfoAsync(Book.Id);
                }
            }

            // Auto-select shelf if none selected (only used by the Shelves sync below)
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

            // Phase 2: sync genres/shelves/tropes BEFORE Phase 3's goal-recalc so that a
            // simultaneous Genre change + Complete-in-the-same-save sees the updated genre
            // associations when `CompleteBookAsync` runs `RecalculateGoalProgressAsync`.
            if (Book.Id != Guid.Empty)
            {
                var currentGenres = await _genreService.GetGenresForBookAsync(Book.Id);
                var currentGenreIds = currentGenres.Select(g => g.Id).ToHashSet();

                foreach (var genreId in currentGenreIds.Where(id => !SelectedGenreIds.Contains(id)))
                {
                    await _genreService.RemoveGenreFromBookAsync(Book.Id, genreId);
                }

                foreach (var genreId in SelectedGenreIds.Where(id => !currentGenreIds.Contains(id)))
                {
                    await _genreService.AddGenreToBookAsync(Book.Id, genreId);
                }

                // Update Shelves — re-fetch with details to see the persisted state even
                // when AddAsync just ran (Book may be fresh without navigation properties).
                var bookWithShelves = await _bookService.GetWithDetailsAsync(Book.Id);
                if (bookWithShelves != null)
                {
                    var currentShelfIds = bookWithShelves.BookShelves.Select(bs => bs.ShelfId).ToHashSet();

                    foreach (var shelfId in currentShelfIds.Where(id => !SelectedShelfIds.Contains(id)))
                    {
                        var shelf = AvailableShelves.FirstOrDefault(s => s.Id == shelfId);
                        if (shelf != null) // Only remove if it's one of the manual shelves we allow editing
                        {
                            await _shelfService.RemoveBookFromShelfAsync(shelfId, Book.Id);
                        }
                    }

                    foreach (var shelfId in SelectedShelfIds.Where(id => !currentShelfIds.Contains(id)))
                    {
                        await _shelfService.AddBookToShelfAsync(shelfId, Book.Id);
                    }
                }

                // Update Tropes
                var currentTropes = await _genreService.GetTropesForBookAsync(Book.Id);
                var currentTropeIds = currentTropes.Select(t => t.Id).ToHashSet();

                foreach (var tropeId in currentTropeIds.Where(id => !SelectedTropeIds.Contains(id)))
                {
                    await _genreService.RemoveTropeFromBookAsync(Book.Id, tropeId);
                }

                foreach (var tropeId in SelectedTropeIds.Where(id => !currentTropeIds.Contains(id)))
                {
                    await _genreService.AddTropeToBookAsync(Book.Id, tropeId);
                }
            }

            // Phase 3: award completion XP + goal recalc AFTER genres are synced.
            // Covers the two completion paths that need XP: a new book added as Completed,
            // or an existing book whose status transitioned to Completed in this save.
            if (createdAsCompleted)
            {
                await _bookService.CompleteBookAsync(Book.Id);
                ShowBookCompletionCelebration = true;
            }
            else if (isBeingCompleted)
            {
                await _bookService.CompleteBookAsync(Book.Id);
                ShowBookCompletionCelebration = true;
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
                return;
            }

            // Fill in the book data
            if (!string.IsNullOrWhiteSpace(metadata.Title))
                Book.Title = metadata.Title;

            if (!string.IsNullOrWhiteSpace(metadata.Author))
                Book.Author = metadata.Author;

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

            LookupMessage = Tr("Lookup_LoadedSuccess");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            LookupMessage = Tr("Lookup_NoInternet");
        }
        catch (HttpRequestException ex) when (IsQuotaExceeded(ex))
        {
            LookupMessage = Tr("Lookup_QuotaReached");
        }
        catch (HttpRequestException ex)
        {
            LookupMessage = ex.StatusCode.HasValue
                ? $"Lookup failed (HTTP {(int)ex.StatusCode.Value}). Please try again."
                : "Lookup failed. Please try again.";
        }
        catch (TaskCanceledException)
        {
            LookupMessage = Tr("Lookup_Timeout");
        }
        catch (Exception ex)
        {
            LookupMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLookingUpIsbn = false;
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
