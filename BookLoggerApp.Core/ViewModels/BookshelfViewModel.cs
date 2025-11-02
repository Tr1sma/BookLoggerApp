using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class BookshelfViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;
    private readonly IPlantService _plantService;

    public BookshelfViewModel(IBookService bookService, IGenreService genreService, IPlantService plantService)
    {
        _bookService = bookService;
        _genreService = genreService;
        _plantService = plantService;
    }

    [ObservableProperty]
    private ObservableCollection<Book> _books = new();

    [ObservableProperty]
    private ObservableCollection<UserPlant> _bookshelfPlants = new();

    [ObservableProperty]
    private ObservableCollection<UserPlant> _availablePlants = new();

    [ObservableProperty]
    private List<Genre> _genres = new();

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private ReadingStatus? _filterStatus;

    [ObservableProperty]
    private Guid? _filterGenreId;

    [ObservableProperty]
    private string _sortBy = "Title"; // Title, Author, DateAdded, Status

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var allBooks = await _bookService.GetAllAsync();
            Books = new ObservableCollection<Book>(allBooks);

            Genres = (await _genreService.GetAllAsync()).ToList();

            // Load plants in bookshelf
            var allPlants = await _plantService.GetAllAsync();
            BookshelfPlants = new ObservableCollection<UserPlant>(
                allPlants.Where(p => p.IsInBookshelf));

            // Load available plants for placement
            AvailablePlants = new ObservableCollection<UserPlant>(
                allPlants.Where(p => !p.IsInBookshelf));
        }, "Failed to load books");
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            IEnumerable<Book> filtered;

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                filtered = await _bookService.GetAllAsync();
            }
            else
            {
                filtered = await _bookService.SearchAsync(SearchQuery);
            }

            // Apply Filters
            if (FilterStatus.HasValue)
            {
                filtered = filtered.Where(b => b.Status == FilterStatus.Value);
            }

            if (FilterGenreId.HasValue)
            {
                var booksInGenre = await _bookService.GetByGenreAsync(FilterGenreId.Value);
                var genreBookIds = booksInGenre.Select(b => b.Id).ToHashSet();
                filtered = filtered.Where(b => genreBookIds.Contains(b.Id));
            }

            // Apply Sorting
            filtered = SortBy switch
            {
                "Author" => filtered.OrderBy(b => b.Author),
                "DateAdded" => filtered.OrderByDescending(b => b.DateAdded),
                "Status" => filtered.OrderBy(b => b.Status),
                _ => filtered.OrderBy(b => b.Title)
            };

            Books = new ObservableCollection<Book>(filtered);
        }, "Failed to search books");
    }

    [RelayCommand]
    public async Task DeleteBookAsync(Guid bookId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _bookService.DeleteAsync(bookId);
            await LoadAsync();
        }, "Failed to delete book");
    }

    [RelayCommand]
    public void ClearFilters()
    {
        SearchQuery = "";
        FilterStatus = null;
        FilterGenreId = null;
    }

    [RelayCommand]
    public async Task PlacePlantInBookshelfAsync((Guid plantId, string position) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var plant = AvailablePlants.FirstOrDefault(p => p.Id == args.plantId);
            if (plant == null)
            {
                SetError("Plant not found");
                return;
            }

            plant.IsInBookshelf = true;
            plant.BookshelfPosition = args.position;
            await _plantService.UpdateAsync(plant);

            // Move from available to bookshelf
            AvailablePlants.Remove(plant);
            BookshelfPlants.Add(plant);
        }, "Failed to place plant");
    }

    [RelayCommand]
    public async Task RemovePlantFromBookshelfAsync(Guid plantId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var plant = BookshelfPlants.FirstOrDefault(p => p.Id == plantId);
            if (plant == null) return;

            plant.IsInBookshelf = false;
            plant.BookshelfPosition = null;
            await _plantService.UpdateAsync(plant);

            // Move from bookshelf to available
            BookshelfPlants.Remove(plant);
            AvailablePlants.Add(plant);
        }, "Failed to remove plant");
    }
}

