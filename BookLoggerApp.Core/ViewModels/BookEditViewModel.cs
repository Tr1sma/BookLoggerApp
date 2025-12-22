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

    public BookEditViewModel(
        IBookService bookService,
        IGenreService genreService,
        ILookupService lookupService,
        IImageService imageService,
        IShelfService shelfService)
    {
        _bookService = bookService;
        _genreService = genreService;
        _lookupService = lookupService;
        _imageService = imageService;
        _shelfService = shelfService;
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

    // Track original status to detect when book becomes completed
    private ReadingStatus? _originalStatus;

    [RelayCommand]
    public async Task LoadAsync(Guid? bookId)
    {
        await ExecuteSafelyAsync(async () =>
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
                    _originalStatus = Book.Status;
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
            }
        }, "Failed to load book");
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Book == null) return;

        // Reset celebration flag
        ShowBookCompletionCelebration = false;

        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Book.Title) || string.IsNullOrWhiteSpace(Book.Author))
            {
                SetError("Title and Author are required");
                return;
            }

            var isNewBook = Book.Id == Guid.Empty || await _bookService.GetByIdAsync(Book.Id) == null;
            var coverImageUrl = Book.CoverImagePath;

            // Check if book is being marked as completed for the first time
            var isBeingCompleted = Book.Status == ReadingStatus.Completed &&
                                   _originalStatus.HasValue &&
                                   _originalStatus.Value != ReadingStatus.Completed;

            if (isNewBook)
            {
                // New book
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

                // If new book is being added as completed, award XP
                if (Book.Status == ReadingStatus.Completed)
                {
                    await _bookService.CompleteBookAsync(Book.Id);
                    ShowBookCompletionCelebration = true;
                }
            }
            else
            {
                // Update existing book
                // Always save changes first to persist any property edits (Title, Author, Spine Mode, etc.)
                await _bookService.UpdateAsync(Book);

                if (isBeingCompleted)
                {
                    // Book is being marked as completed - use CompleteBookAsync for XP and side effects
                    await _bookService.CompleteBookAsync(Book.Id);
                    ShowBookCompletionCelebration = true;
                }
            }

            // Update genres
            if (Book.Id != Guid.Empty)
            {
                var currentGenres = await _genreService.GetGenresForBookAsync(Book.Id);
                var currentGenreIds = currentGenres.Select(g => g.Id).ToHashSet();

                // Remove genres that are no longer selected
                foreach (var genreId in currentGenreIds.Where(id => !SelectedGenreIds.Contains(id)))
                {
                    await _genreService.RemoveGenreFromBookAsync(Book.Id, genreId);
                }

                // Add new genres
                foreach (var genreId in SelectedGenreIds.Where(id => !currentGenreIds.Contains(id)))
                {
                    await _genreService.AddGenreToBookAsync(Book.Id, genreId);
                }

                // Update Shelves (Similar logic)
                // Note: GetWithDetailsAsync was called in Load, but Book might be fresh if AddAsync was just called
                // We should re-fetch with details to be safe or rely on what we have if loaded
                var bookWithShelves = await _bookService.GetWithDetailsAsync(Book.Id);
                if (bookWithShelves != null)
                {
                    var currentShelfIds = bookWithShelves.BookShelves.Select(bs => bs.ShelfId).ToHashSet();

                    // Remove from deselected manual shelves
                    foreach (var shelfId in currentShelfIds.Where(id => !SelectedShelfIds.Contains(id)))
                    {
                        // Verify it is a manual shelf? (UI only shows manual, but safety check or just assume)
                        // For now assume filtering happened in UI/ViewModel
                        var shelf = AvailableShelves.FirstOrDefault(s => s.Id == shelfId);
                        if (shelf != null) // Only remove if it's one of the manual shelves we allow editing
                        {
                            await _shelfService.RemoveBookFromShelfAsync(shelfId, Book.Id);
                        }
                    }

                    // Add to newly selected shelves
                    foreach (var shelfId in SelectedShelfIds.Where(id => !currentShelfIds.Contains(id)))
                    {
                        await _shelfService.AddBookToShelfAsync(shelfId, Book.Id);
                    }
                }
            }
        }, "Failed to save book");
    }

    [RelayCommand]
    public async Task LookupByIsbnAsync()
    {
        if (Book == null || string.IsNullOrWhiteSpace(Book.ISBN))
        {
            LookupMessage = "Please enter an ISBN first";
            return;
        }

        IsLookingUpIsbn = true;
        LookupMessage = null;

        try
        {
            var metadata = await _lookupService.LookupByISBNAsync(Book.ISBN);

            if (metadata == null)
            {
                LookupMessage = "No book found with this ISBN";
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

            LookupMessage = "Book data loaded successfully!";
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
}

