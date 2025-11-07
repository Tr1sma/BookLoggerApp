using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class BookDetailViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IProgressService _progressService;
    private readonly IQuoteService _quoteService;
    private readonly IAnnotationService _annotationService;
    private readonly IGenreService _genreService;

    public BookDetailViewModel(
        IBookService bookService,
        IProgressService progressService,
        IQuoteService quoteService,
        IAnnotationService annotationService,
        IGenreService genreService)
    {
        _bookService = bookService;
        _progressService = progressService;
        _quoteService = quoteService;
        _annotationService = annotationService;
        _genreService = genreService;
    }

    [ObservableProperty]
    private Book? _book;

    [ObservableProperty]
    private int _totalMinutes;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private ObservableCollection<ReadingSession> _sessions = new();

    [ObservableProperty]
    private ObservableCollection<Quote> _quotes = new();

    [ObservableProperty]
    private ObservableCollection<Annotation> _annotations = new();

    [ObservableProperty]
    private List<Genre> _bookGenres = new();

    [RelayCommand]
    public async Task LoadAsync(Guid bookId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            Book = await _bookService.GetWithDetailsAsync(bookId);
            if (Book == null)
            {
                SetError("Book not found");
                return;
            }

            TotalMinutes = await _progressService.GetTotalMinutesAsync(bookId);
            TotalPages = await _progressService.GetTotalPagesAsync(bookId);

            var sessions = await _progressService.GetSessionsByBookAsync(bookId);
            Sessions = new ObservableCollection<ReadingSession>(sessions);

            var quotes = await _quoteService.GetQuotesByBookAsync(bookId);
            Quotes = new ObservableCollection<Quote>(quotes);

            var annotations = await _annotationService.GetAnnotationsByBookAsync(bookId);
            Annotations = new ObservableCollection<Annotation>(annotations);

            BookGenres = (await _genreService.GetGenresForBookAsync(bookId)).ToList();
        }, "Failed to load book details");
    }

    [RelayCommand]
    public async Task StartReadingAsync()
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            await _bookService.StartReadingAsync(Book.Id);
            await LoadAsync(Book.Id); // Reload
        }, "Failed to start reading");
    }

    [RelayCommand]
    public async Task CompleteBookAsync()
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            await _bookService.CompleteBookAsync(Book.Id);
            await LoadAsync(Book.Id); // Reload
        }, "Failed to complete book");
    }

    public async Task AddQuoteAsync(string text, int? pageNumber)
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            var quote = new Quote
            {
                BookId = Book.Id,
                Text = text,
                PageNumber = pageNumber
            };
            await _quoteService.AddAsync(quote);
            await LoadAsync(Book.Id); // Reload
        }, "Failed to add quote");
    }

    [RelayCommand]
    public async Task AddSessionAsync(int minutes)
    {
        if (Book == null || minutes <= 0) return;

        await ExecuteSafelyAsync(async () =>
        {
            await _progressService.AddSessionAsync(new ReadingSession
            {
                BookId = Book.Id,
                Minutes = minutes,
                StartedAt = DateTime.UtcNow
            });
            await LoadAsync(Book.Id); // Reload
        }, "Failed to add session");
    }

    /// <summary>
    /// Updates a specific rating category for the current book.
    /// </summary>
    public async Task UpdateRatingAsync(RatingCategory category, int? rating)
    {
        if (Book == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            // Update the appropriate rating property
            switch (category)
            {
                case RatingCategory.Characters:
                    Book.CharactersRating = rating;
                    break;
                case RatingCategory.Plot:
                    Book.PlotRating = rating;
                    break;
                case RatingCategory.WritingStyle:
                    Book.WritingStyleRating = rating;
                    break;
                case RatingCategory.SpiceLevel:
                    Book.SpiceLevelRating = rating;
                    break;
                case RatingCategory.Pacing:
                    Book.PacingRating = rating;
                    break;
                case RatingCategory.WorldBuilding:
                    Book.WorldBuildingRating = rating;
                    break;
                case RatingCategory.Overall:
                    Book.OverallRating = rating;
                    break;
            }

            // Save the book
            await _bookService.UpdateAsync(Book);

            // Reload to refresh computed properties
            await LoadAsync(Book.Id);
        }, $"Failed to update {category} rating");
    }

    /// <summary>
    /// Gets the rating for a specific category.
    /// </summary>
    public int? GetRating(RatingCategory category)
    {
        if (Book == null) return null;

        return category switch
        {
            RatingCategory.Characters => Book.CharactersRating,
            RatingCategory.Plot => Book.PlotRating,
            RatingCategory.WritingStyle => Book.WritingStyleRating,
            RatingCategory.SpiceLevel => Book.SpiceLevelRating,
            RatingCategory.Pacing => Book.PacingRating,
            RatingCategory.WorldBuilding => Book.WorldBuildingRating,
            RatingCategory.Overall => Book.OverallRating,
            _ => null
        };
    }
}
