using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.ViewModels;

public partial class BookshelfViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;
    private readonly IPlantService _plantService;
    private readonly IGoalService _goalService;

    public BookshelfViewModel(IBookService bookService, IGenreService genreService, IPlantService plantService, IGoalService goalService)
    {
        _bookService = bookService;
        _genreService = genreService;
        _plantService = plantService;
        _goalService = goalService;
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

    // Goal tracking properties
    [ObservableProperty]
    private int _tbrCount = 0;

    [ObservableProperty]
    private int _goalTarget = 0;

    [ObservableProperty]
    private int _booksReadThisYear = 0;

    [ObservableProperty]
    private int _booksRemainingToGoal = 0;

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

            // Calculate goal statistics
            await CalculateGoalStatsAsync();
        }, "Failed to load books");
    }

    private async Task CalculateGoalStatsAsync()
    {
        // Count TBR (To Be Read) books - those with "Planned" status
        TbrCount = Books.Count(b => b.Status == ReadingStatus.Planned);

        // Get active yearly book goal
        var activeGoals = await _goalService.GetActiveGoalsAsync();
        var yearlyBookGoal = activeGoals
            .Where(g => g.Type == GoalType.Books)
            .Where(g => g.StartDate.Year == DateTime.Now.Year || g.EndDate.Year == DateTime.Now.Year)
            .OrderByDescending(g => g.Target)
            .FirstOrDefault();

        if (yearlyBookGoal != null)
        {
            GoalTarget = yearlyBookGoal.Target;

            // Count books read this year (completed status and completed this year)
            BooksReadThisYear = Books.Count(b =>
                b.Status == ReadingStatus.Completed &&
                b.DateCompleted.HasValue &&
                b.DateCompleted.Value.Year == DateTime.Now.Year);

            // Calculate books remaining
            BooksRemainingToGoal = GoalTarget - BooksReadThisYear;
        }
        else
        {
            // No active goal found
            GoalTarget = 0;
            BooksReadThisYear = Books.Count(b =>
                b.Status == ReadingStatus.Completed &&
                b.DateCompleted.HasValue &&
                b.DateCompleted.Value.Year == DateTime.Now.Year);
            BooksRemainingToGoal = 0;
        }
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

    [RelayCommand]
    public async Task WaterPlantAsync(Guid plantId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.WaterPlantAsync(plantId);

            // Refresh plant data
            var plant = BookshelfPlants.FirstOrDefault(p => p.Id == plantId);
            if (plant != null)
            {
                var updatedPlant = await _plantService.GetByIdAsync(plantId);
                if (updatedPlant != null)
                {
                    var index = BookshelfPlants.IndexOf(plant);
                    BookshelfPlants[index] = updatedPlant;
                }
            }
        }, "Failed to water plant");
    }

    [RelayCommand]
    public async Task DeletePlantAsync(Guid plantId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.DeleteAsync(plantId);

            // Remove from bookshelf or available lists
            var plantInBookshelf = BookshelfPlants.FirstOrDefault(p => p.Id == plantId);
            if (plantInBookshelf != null)
            {
                BookshelfPlants.Remove(plantInBookshelf);
            }

            var plantAvailable = AvailablePlants.FirstOrDefault(p => p.Id == plantId);
            if (plantAvailable != null)
            {
                AvailablePlants.Remove(plantAvailable);
            }
        }, "Failed to delete plant");
    }

    [RelayCommand]
    public async Task MovePlantToPositionAsync((Guid plantId, string position) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var plant = BookshelfPlants.FirstOrDefault(p => p.Id == args.plantId);
            if (plant == null)
            {
                SetError("Plant not found");
                return;
            }

            plant.BookshelfPosition = args.position;
            await _plantService.UpdateAsync(plant);

            // Reload to reflect new positions
            await LoadAsync();
        }, "Failed to move plant");
    }

    [RelayCommand]
    public async Task MoveBookToPositionAsync((Guid bookId, string position) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var book = Books.FirstOrDefault(b => b.Id == args.bookId);
            if (book == null)
            {
                SetError("Book not found");
                return;
            }

            book.BookshelfPosition = args.position;
            await _bookService.UpdateAsync(book);

            // Reload to reflect new positions
            await LoadAsync();
        }, "Failed to move book");
    }
}

