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

    private readonly IShelfService _shelfService;

    public BookshelfViewModel(IBookService bookService, IGenreService genreService, IPlantService plantService, IGoalService goalService, IShelfService shelfService)
    {
        _bookService = bookService;
        _genreService = genreService;
        _plantService = plantService;
        _goalService = goalService;
        _shelfService = shelfService;
    }

    [ObservableProperty]
    private ObservableCollection<ShelfViewModel> _shelves = new();

    // Keep this for flat search results if needed, or remove? 
    // For now, let's keep it but primarily use Shelves.
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
            // Load Shelves
            var shelves = await _shelfService.GetAllShelvesAsync();
            
            // If no shelves exist, create default ones
            // If no shelves exist, create default ones
            if (!shelves.Any())
            {
                var defaultShelf = new Shelf { Name = "Main Shelf", SortOrder = 0 };
                await _shelfService.CreateShelfAsync(defaultShelf);
                shelves = await _shelfService.GetAllShelvesAsync();
            }

            // AUTO-MIGRATION: Ensure all books are on at least one shelf
            // This specifically fixes the issue where pre-existing books disappear from the UI
            // because they haven't been assigned to the new shelf system yet.
            var allBooks = await _bookService.GetAllAsync();
            var mainShelf = shelves.FirstOrDefault();
            
            if (mainShelf != null && allBooks.Any())
            {
                // Get set of all book IDs currently in any shelf
                var assignedBookIds = new HashSet<Guid>();
                foreach (var shelf in shelves)
                {
                    var shelfBooks = await _shelfService.GetBooksForShelfAsync(shelf.Id);
                    foreach (var book in shelfBooks)
                    {
                        assignedBookIds.Add(book.Id);
                    }
                }

                // Identify orphans (books not in any shelf)
                var orphanBooks = allBooks.Where(b => !assignedBookIds.Contains(b.Id)).ToList();
                
                // Assign orphans to the main shelf
                if (orphanBooks.Any())
                {
                    foreach (var orphan in orphanBooks)
                    {
                        await _shelfService.AddBookToShelfAsync(mainShelf.Id, orphan.Id);
                    }
                }
            }

            var shelfViewModels = new List<ShelfViewModel>();
            foreach (var shelf in shelves)
            {
                var books = await _shelfService.GetBooksForShelfAsync(shelf.Id);
                shelfViewModels.Add(new ShelfViewModel { Shelf = shelf, Books = new ObservableCollection<Book>(books) });
            }
            Shelves = new ObservableCollection<ShelfViewModel>(shelfViewModels);

            // Also load flat list for calculations (like TBR count)
            // Variable allBooks already loaded above
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

    // New Commands for Shelf Management
    [RelayCommand]
    public async Task CreateShelfAsync((string name, ShelfAutoSortRule rule) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var newShelf = new Shelf
            {
                Name = args.name,
                AutoSortRule = args.rule
            };
            await _shelfService.CreateShelfAsync(newShelf);
            await LoadAsync();
        }, "Failed to create shelf");
    }

    [RelayCommand]
    public async Task DeleteShelfAsync(Guid shelfId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.DeleteShelfAsync(shelfId);
            await LoadAsync();
        }, "Failed to delete shelf");
    }

    [RelayCommand]
    public async Task MoveBookToShelfAsync((Guid bookId, Guid targetShelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
             // Implement moving logic: 
             // If book is already in a shelf, remove it? Or just add?
             // Requirement says "Book can be in multiple shelves".
             // But drag and drop usually implies "move" if within the same context, or "copy" if distinct.
             // User request: "Ein buch darf gleichzeitig in mehreren regalen sein." (A book may be in multiple shelves at the same time).
             
             // So, standard drag and drop might be "Add to shelf". 
             // But if dragging FROM a shelf TO another, user might expect move.
             // Let's assume Add for now, or check if we know the source shelf.
             
             await _shelfService.AddBookToShelfAsync(args.targetShelfId, args.bookId);
             
             // Refresh
             await LoadAsync();
        }, "Failed to move book to shelf");
    }
    
    [RelayCommand]
    public async Task RemoveBookFromShelfAsync((Guid bookId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.RemoveBookFromShelfAsync(args.shelfId, args.bookId);
            await LoadAsync();
        }, "Failed to remove book from shelf");
    }

    // ... (Keep existing goal stats and search logic, update Search to filter shelves maybe?)

    private async Task CalculateGoalStatsAsync()
    {
        // ... (Keep existing logic using _books which contains all books)
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
        // Search logic might need to filter books within shelves or show a "Search Results" virtual shelf?
        // For now, let's keep search acting on the global book list, 
        // effectively showing "Search Results" and hiding standard shelves if search is active.
        
        await ExecuteSafelyAsync(async () =>
        {
             IEnumerable<Book> filtered;

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                // Reset to shelf view
                await LoadAsync();
                return;
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
            // Clear shelves to indicate search mode
            Shelves.Clear();
            
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
    public async Task ClearFilters()
    {
        SearchQuery = "";
        FilterStatus = null;
        FilterGenreId = null;
        await LoadAsync(); // Reload to show shelves again
    }

    // ... (Keep existing plant commands)
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
        // This is legacy single-shelf positioning. 
        // We might want to adapt this to shelf-specific positioning later.
        // For now, keep it compatible or ignore if not using this field anymore.
        
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

public partial class ShelfViewModel : ObservableObject
{
    [ObservableProperty]
    private Shelf _shelf = null!;

    [ObservableProperty]
    private ObservableCollection<Book> _books = new();
}




