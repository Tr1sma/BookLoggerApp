using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class BookEditViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;

    public BookEditViewModel(IBookService bookService, IGenreService genreService)
    {
        _bookService = bookService;
        _genreService = genreService;
    }

    [ObservableProperty]
    private Book? _book;

    [ObservableProperty]
    private List<Genre> _availableGenres = new();

    [ObservableProperty]
    private List<Guid> _selectedGenreIds = new();

    [RelayCommand]
    public async Task LoadAsync(Guid? bookId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            AvailableGenres = (await _genreService.GetAllAsync()).ToList();

            if (bookId.HasValue)
            {
                Book = await _bookService.GetWithDetailsAsync(bookId.Value);
                if (Book != null)
                {
                    SelectedGenreIds = Book.BookGenres.Select(bg => bg.GenreId).ToList();
                }
            }
            else
            {
                // New book
                Book = new Book
                {
                    Status = ReadingStatus.Planned,
                    DateAdded = DateTime.UtcNow
                };
            }
        }, "Failed to load book");
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Book.Title) || string.IsNullOrWhiteSpace(Book.Author))
            {
                SetError("Title and Author are required");
                return;
            }

            if (Book.Id == Guid.Empty || await _bookService.GetByIdAsync(Book.Id) == null)
            {
                // New book
                Book = await _bookService.AddAsync(Book);
            }
            else
            {
                // Update existing
                await _bookService.UpdateAsync(Book);
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
            }
        }, "Failed to save book");
    }
}

