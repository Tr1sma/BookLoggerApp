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
        await ExecuteSafelyWithDbAsync(async () =>
        {
            // 1. Fetch data
            var shelves = await _shelfService.GetAllShelvesAsync();
            var allBooks = await _bookService.GetAllAsync(); // Still needed for Search/Filter? Or just rely on shelves?
            // Note: GetAllShelvesAsync fetches light objects? 
            // We need full details for items. The ShelfService methods like GetBooksForShelfAsync might be needed,
            // OR we rely on GetShelfByIdAsync having Includes. 
            // _shelfService.GetAllShelvesAsync usually returns List<Shelf>. 
            // We should iterate and fetch details or update GetAll to include relationships.
            // Let's assume we iterate and fetch details to be safe and get fresh data.

            if (!shelves.Any())
            {
                var defaultShelf = new Shelf { Name = "Main Shelf", SortOrder = 0 };
                await _shelfService.CreateShelfAsync(defaultShelf);
                shelves = await _shelfService.GetAllShelvesAsync();
            }

            var shelfViewModels = new List<ShelfViewModel>();
            var plantsOnShelvesIds = new HashSet<Guid>();

            // 2. Migration Check: Fetch legacy plants
            var allPlants = await _plantService.GetAllAsync();
            var legacyPlants = allPlants.Where(p => p.IsInBookshelf).ToList();

            // If we have legacy plants but they aren't linked to shelves via PlantShelf, migrate safely.
            // We can check if they are in the fetched shelves' properties?
            // Actually, we need to inspect the PlantShelf table. 
            // For simplicity: If a plant has IsInBookshelf=true, we ensure it's on a shelf.
            // BUT we don't have direct access to check PlantShelf existence easily here without fetching.
            // So let's handle it during shelf construction.

            foreach (var shelf in shelves)
            {
                // Fetch full shelf with items (Books and Plants)
                // We use GetShelfByIdAsync ensures we get the relations including BookShelves and PlantShelves
                var fullShelf = await _shelfService.GetShelfByIdAsync(shelf.Id);

                if (fullShelf == null) continue;

                var items = new List<ShelfItemViewModel>();

                // Books - For Auto-Sort shelves, we need to use GetBooksForShelfAsync
                // which dynamically filters books by status instead of using BookShelves relation
                if (fullShelf.AutoSortRule != ShelfAutoSortRule.None)
                {
                    // Auto-Sort shelf: Get books dynamically based on status
                    var booksForShelf = await _shelfService.GetBooksForShelfAsync(fullShelf.Id);
                    int position = 0;
                    foreach (var book in booksForShelf)
                    {
                        items.Add(new ShelfItemViewModel(book, position++));
                    }
                }
                else
                {
                    // Manual shelf: Use the BookShelves relation
                    foreach (var bookShelf in fullShelf.BookShelves)
                    {
                        if (bookShelf.Book != null)
                        {
                            items.Add(new ShelfItemViewModel(bookShelf.Book, bookShelf.Position));
                        }
                    }
                }

                // Plants
                foreach (var plantShelf in fullShelf.PlantShelves)
                {
                    if (plantShelf.Plant != null)
                    {
                        items.Add(new ShelfItemViewModel(plantShelf.Plant, plantShelf.Position));
                        plantsOnShelvesIds.Add(plantShelf.Plant.Id);
                    }
                }

                // Sort
                var sortedItems = items.OrderBy(i => i.Position).ToList();

                shelfViewModels.Add(new ShelfViewModel
                {
                    Shelf = fullShelf,
                    Items = new ObservableCollection<ShelfItemViewModel>(sortedItems)
                });
            }

            // 3. Migration: Check for orphan legacy plants
            // Migrate them to the first shelf without recursive reload.
            // The migrated plants are added to the in-memory data directly.
            var firstShelf = shelfViewModels.FirstOrDefault();
            if (firstShelf != null)
            {
                foreach (var legacyPlant in legacyPlants)
                {
                    if (!plantsOnShelvesIds.Contains(legacyPlant.Id))
                    {
                        await _shelfService.AddPlantToShelfAsync(firstShelf.Shelf.Id, legacyPlant.Id);
                        // Reflect migration in the in-memory model immediately
                        int nextPos = firstShelf.Items.Count;
                        firstShelf.Items.Add(new ShelfItemViewModel(legacyPlant, nextPos));
                        plantsOnShelvesIds.Add(legacyPlant.Id);
                    }
                }
            }

            Shelves = new ObservableCollection<ShelfViewModel>(shelfViewModels);
            Books = new ObservableCollection<Book>(allBooks); // For search access

            // 4. Available Plants
            // Filter out plants that are already on ANY shelf
            AvailablePlants = new ObservableCollection<UserPlant>(
                allPlants.Where(p => !plantsOnShelvesIds.Contains(p.Id))
            );

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
    public async Task MoveShelfUpAsync(Guid shelfId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var shelfIndex = Shelves.ToList().FindIndex(s => s.Shelf.Id == shelfId);
            if (shelfIndex > 0)
            {
                var shelfToMove = Shelves[shelfIndex];
                var shelfAbove = Shelves[shelfIndex - 1];

                // Swap in the local list
                Shelves.Move(shelfIndex, shelfIndex - 1);

                // Persist new order
                var newOrderIds = Shelves.Select(s => s.Shelf.Id).ToList();
                await _shelfService.ReorderShelvesAsync(newOrderIds);
            }
        }, "Failed to move shelf up");
    }

    [RelayCommand]
    public async Task MoveShelfDownAsync(Guid shelfId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var shelfIndex = Shelves.ToList().FindIndex(s => s.Shelf.Id == shelfId);
            if (shelfIndex >= 0 && shelfIndex < Shelves.Count - 1)
            {
                var shelfToMove = Shelves[shelfIndex];
                var shelfBelow = Shelves[shelfIndex + 1];

                // Swap in the local list
                Shelves.Move(shelfIndex, shelfIndex + 1);

                // Persist new order
                var newOrderIds = Shelves.Select(s => s.Shelf.Id).ToList();
                await _shelfService.ReorderShelvesAsync(newOrderIds);
            }
        }, "Failed to move shelf down");
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
        // Count TBR (To Be Read) books - those with "Planned" status
        TbrCount = Books.Count(b => b.Status == ReadingStatus.Planned);

        // Use UtcNow consistently — goals and DateCompleted are stored in UTC
        int currentYear = DateTime.UtcNow.Year;

        // GetActiveGoalsAsync already calculates correct progress (goal.Current)
        // accounting for excluded books, genre filters, and exact date ranges
        var activeGoals = await _goalService.GetActiveGoalsAsync();
        var yearlyBookGoal = activeGoals
            .Where(g => g.Type == GoalType.Books)
            .Where(g => g.StartDate.Year == currentYear || g.EndDate.Year == currentYear)
            .OrderByDescending(g => g.Target)
            .FirstOrDefault();

        if (yearlyBookGoal != null)
        {
            GoalTarget = yearlyBookGoal.Target;
            BooksReadThisYear = yearlyBookGoal.Current;
            BooksRemainingToGoal = GoalTarget - BooksReadThisYear;
        }
        else
        {
            GoalTarget = 0;
            BooksReadThisYear = 0;
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
    public async Task AddPlantToShelfAsync((Guid plantId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var plant = AvailablePlants.FirstOrDefault(p => p.Id == args.plantId);
            if (plant == null)
            {
                // It might be available but not in the observable list if we didn't reload?
                // Or maybe we just trust the ID.
                // Let's reload logic.
            }

            // Use the new service method
            await _shelfService.AddPlantToShelfAsync(args.shelfId, args.plantId);

            // Refresh
            await LoadAsync();
        }, "Failed to place plant");
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
    public async Task WaterPlantAsync(Guid plantId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.WaterPlantAsync(plantId);
            // Reload to reflect status changes
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

    // Dropping "MovePlantToPositionAsync" in favor of generic Drag/Drop reordering if possible
    // or adapting it later. For now, removing the legacy string-based position logic.


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
    public async Task ReorderShelfItemsAsync((Guid shelfId, Guid sourceId, Guid targetId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var shelfVM = Shelves.FirstOrDefault(s => s.Shelf.Id == args.shelfId);
            if (shelfVM == null) return;

            var sourceItem = shelfVM.Items.FirstOrDefault(i => i.Id == args.sourceId);
            var targetItem = shelfVM.Items.FirstOrDefault(i => i.Id == args.targetId);

            if (sourceItem == null || targetItem == null || sourceItem == targetItem) return;

            var oldIndex = shelfVM.Items.IndexOf(sourceItem);
            var newIndex = shelfVM.Items.IndexOf(targetItem);

            if (oldIndex < 0 || newIndex < 0) return;

            // Move in ObservableCollection
            shelfVM.Items.Move(oldIndex, newIndex);

            // Recalculate positions
            var bookPositions = new Dictionary<Guid, int>();
            var plantPositions = new Dictionary<Guid, int>();

            for (int i = 0; i < shelfVM.Items.Count; i++)
            {
                var item = shelfVM.Items[i];
                item.Position = i; // Update ViewModel position

                if (item.Type == ShelfItemType.Book)
                    bookPositions[item.Id] = i;
                else if (item.Type == ShelfItemType.Plant)
                    plantPositions[item.Id] = i;
            }

            // Persist
            await _shelfService.UpdateShelfPositionsAsync(args.shelfId, bookPositions, plantPositions);

        }, "Failed to reorder items");
    }

    [RelayCommand]
    public async Task MoveItemBetweenShelvesAsync(
        (Guid sourceShelfId, Guid targetShelfId, Guid itemId, ShelfItemType type, int position) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (args.type == ShelfItemType.Book)
                await _shelfService.MoveBookBetweenShelvesAsync(
                    args.sourceShelfId, args.targetShelfId, args.itemId, args.position);
            else
                await _shelfService.MovePlantBetweenShelvesAsync(
                    args.sourceShelfId, args.targetShelfId, args.itemId, args.position);

            await LoadAsync();
        }, "Failed to move item between shelves");
    }

    /// <summary>
    /// Optimistic in-memory move for instant visual feedback during drag-and-drop.
    /// Does NOT persist to DB — call PersistCrossShelfMoveAsync afterward.
    /// </summary>
    public void MoveItemBetweenShelvesInMemory(
        Guid sourceShelfId, Guid targetShelfId, Guid itemId, ShelfItemType type, int position)
    {
        var sourceShelf = Shelves.FirstOrDefault(s => s.Shelf.Id == sourceShelfId);
        var targetShelf = Shelves.FirstOrDefault(s => s.Shelf.Id == targetShelfId);
        if (sourceShelf == null || targetShelf == null) return;

        var item = sourceShelf.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return;

        // Remove from source
        sourceShelf.Items.Remove(item);

        // Recalculate source positions
        for (int i = 0; i < sourceShelf.Items.Count; i++)
            sourceShelf.Items[i].Position = i;

        // Insert into target at the right position
        int insertAt = position >= 0 && position <= targetShelf.Items.Count
            ? position
            : targetShelf.Items.Count;

        targetShelf.Items.Insert(insertAt, item);

        // Recalculate target positions
        for (int i = 0; i < targetShelf.Items.Count; i++)
            targetShelf.Items[i].Position = i;
    }

    /// <summary>
    /// Persists a cross-shelf move to the database without reloading all shelves.
    /// </summary>
    public async Task PersistCrossShelfMoveAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid itemId, ShelfItemType type, int position)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (type == ShelfItemType.Book)
                await _shelfService.MoveBookBetweenShelvesAsync(
                    sourceShelfId, targetShelfId, itemId, position);
            else
                await _shelfService.MovePlantBetweenShelvesAsync(
                    sourceShelfId, targetShelfId, itemId, position);
        }, "Failed to persist shelf move");
    }

    /// <summary>
    /// Refreshes only the goal header stats without reloading all shelf data.
    /// </summary>
    public async Task RefreshGoalStatsAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            await CalculateGoalStatsAsync();
        }, "Failed to refresh goal stats");
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




