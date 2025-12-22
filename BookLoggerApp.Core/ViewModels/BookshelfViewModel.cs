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

    // Plants that are NOT on any shelf
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
                var items = await _shelfService.GetShelfItemsAsync(shelf.Id);
                var viewModels = items.Select(i => new ShelfItemViewModel(i)).ToList();
                shelfViewModels.Add(new ShelfViewModel { Shelf = shelf, Items = new ObservableCollection<ShelfItemViewModel>(viewModels) });
            }
            Shelves = new ObservableCollection<ShelfViewModel>(shelfViewModels);

            // Also load flat list for calculations (like TBR count)
            // Variable allBooks already loaded above
            Books = new ObservableCollection<Book>(allBooks);

            Genres = (await _genreService.GetAllAsync()).ToList();

            // Load available plants for placement (those not in any PlantShelf)
            var allPlants = await _plantService.GetAllAsync();
            AvailablePlants = new ObservableCollection<UserPlant>(
                allPlants.Where(p => !p.PlantShelves.Any()));

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

    [RelayCommand]
    public async Task AddPlantToShelfAsync((Guid plantId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.AddPlantToShelfAsync(args.shelfId, args.plantId);
            await LoadAsync();
        }, "Failed to add plant");
    }

    [RelayCommand]
    public async Task RemovePlantFromShelfAsync((Guid plantId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.RemovePlantFromShelfAsync(args.shelfId, args.plantId);
            await LoadAsync();
        }, "Failed to remove plant");
    }

    [RelayCommand]
    public async Task UpdateShelfOrderAsync((Guid shelfId, List<ShelfItemViewModel> items) args)
    {
         await ExecuteSafelyAsync(async () =>
         {
             var dtos = args.items.Select((vm, index) => new ShelfItemDto
             {
                 ItemId = vm.ItemId,
                 Type = vm.Type,
                 Position = index
             }).ToList();

             await _shelfService.ReorderShelfItemsAsync(args.shelfId, dtos);
         }, "Failed to update order");
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

    [RelayCommand]
    public async Task WaterPlantAsync(Guid plantId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.WaterPlantAsync(plantId);

            await LoadAsync();
        }, "Failed to water plant");
    }

    [RelayCommand]
    public async Task DeletePlantAsync(Guid plantId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.DeleteAsync(plantId);
            await LoadAsync();
        }, "Failed to delete plant");
    }
}

public partial class ShelfViewModel : ObservableObject
{
    [ObservableProperty]
    private Shelf _shelf = null!;

    [ObservableProperty]
    private ObservableCollection<ShelfItemViewModel> _items = new();

    [ObservableProperty]
    private bool _isExpanded = true;
}
