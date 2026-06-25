using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Entitlements;
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
    private readonly IDecorationService _decorationService;

    private readonly IShelfService _shelfService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IFeatureGuard? _featureGuard;

    public BookshelfViewModel(IBookService bookService, IGenreService genreService, IPlantService plantService, IGoalService goalService, IDecorationService decorationService, IShelfService shelfService, IAppSettingsProvider settingsProvider, IFeatureGuard? featureGuard = null)
    {
        _bookService = bookService;
        _genreService = genreService;
        _plantService = plantService;
        _goalService = goalService;
        _decorationService = decorationService;
        _shelfService = shelfService;
        _settingsProvider = settingsProvider;
        _featureGuard = featureGuard;
    }

    [ObservableProperty]
    private ObservableCollection<ShelfViewModel> _shelves = new();

    // Flat list backing search/filter; the shelf view is the primary surface.
    [ObservableProperty]
    private ObservableCollection<Book> _books = new();

    [ObservableProperty]
    private ObservableCollection<UserPlant> _availablePlants = new();

    [ObservableProperty]
    private ObservableCollection<UserDecoration> _availableDecorations = new();

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

    [ObservableProperty]
    private int _tbrCount = 0;

    [ObservableProperty]
    private int _goalTarget = 0;

    [ObservableProperty]
    private int _booksReadThisYear = 0;

    [ObservableProperty]
    private int _booksRemainingToGoal = 0;

    [ObservableProperty]
    private string _shelfLedgeColor = "#8B7355";

    [ObservableProperty]
    private string _shelfBaseColor = "#D4A574";

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            await _plantService.UpdatePlantStatusesAsync(ct);

            // Custom shelf colors are a Plus feature; unentitled users see defaults while saved values stay in AppSettings.
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            bool customColorsAllowed = _featureGuard?.HasAccess(FeatureKey.CustomShelfColors) ?? true;
            var defaultColors = new AppSettings();
            ShelfLedgeColor = customColorsAllowed ? settings.ShelfLedgeColor : defaultColors.ShelfLedgeColor;
            ShelfBaseColor = customColorsAllowed ? settings.ShelfBaseColor : defaultColors.ShelfBaseColor;

            var shelves = await _shelfService.GetAllShelvesAsync();
            var allBooks = await _bookService.GetAllAsync(ct); // Used by search/filter.

            if (!shelves.Any())
            {
                var defaultShelf = new Shelf { Name = "Main Shelf", SortOrder = 0 };
                await _shelfService.CreateShelfAsync(defaultShelf);
                shelves = await _shelfService.GetAllShelvesAsync();
            }

            var shelfViewModels = new List<ShelfViewModel>();
            var plantsOnShelvesIds = new HashSet<Guid>();
            var decorationsOnShelvesIds = new HashSet<Guid>();

            // Legacy plants (IsInBookshelf) get migrated onto a shelf during construction below.
            var allPlants = await _plantService.GetAllAsync(ct);
            var legacyPlants = allPlants
                .Where(p => p.IsInBookshelf && p.Status != PlantStatus.Dead)
                .ToList();

            foreach (var shelf in shelves)
            {
                // GetShelfByIdAsync includes BookShelves/PlantShelves relations.
                var fullShelf = await _shelfService.GetShelfByIdAsync(shelf.Id);

                if (fullShelf == null) continue;

                var items = new List<ShelfItemViewModel>();

                // Auto-Sort shelves filter books dynamically by status; manual shelves use the BookShelves relation.
                if (fullShelf.AutoSortRule != ShelfAutoSortRule.None)
                {
                    var booksForShelf = await _shelfService.GetBooksForShelfAsync(fullShelf.Id);
                    int position = 0;
                    foreach (var book in booksForShelf)
                    {
                        items.Add(new ShelfItemViewModel(book, position++));
                    }
                }
                else
                {
                    foreach (var bookShelf in fullShelf.BookShelves)
                    {
                        if (bookShelf.Book != null)
                        {
                            items.Add(new ShelfItemViewModel(bookShelf.Book, bookShelf.Position));
                        }
                    }
                }

                foreach (var plantShelf in fullShelf.PlantShelves)
                {
                    if (plantShelf.Plant != null)
                    {
                        int plantSlotWidth = 1;
                        items.Add(new ShelfItemViewModel(plantShelf.Plant, plantShelf.Position, plantSlotWidth));
                        plantsOnShelvesIds.Add(plantShelf.Plant.Id);
                    }
                }

                foreach (var decorationShelf in fullShelf.DecorationShelves)
                {
                    if (decorationShelf.Decoration != null)
                    {
                        int decoSlotWidth = decorationShelf.Decoration.ShopItem?.SlotWidth ?? 1;
                        items.Add(new ShelfItemViewModel(decorationShelf.Decoration, decorationShelf.Position, decoSlotWidth));
                        decorationsOnShelvesIds.Add(decorationShelf.Decoration.Id);
                    }
                }

                var sortedItems = items.OrderBy(i => i.Position).ToList();

                shelfViewModels.Add(new ShelfViewModel
                {
                    Shelf = fullShelf,
                    Items = new ObservableCollection<ShelfItemViewModel>(sortedItems)
                });
            }

            // Migrate orphan legacy plants onto the first shelf, updating the in-memory model directly.
            var firstShelf = shelfViewModels.FirstOrDefault();
            if (firstShelf != null)
            {
                foreach (var legacyPlant in legacyPlants)
                {
                    if (!plantsOnShelvesIds.Contains(legacyPlant.Id))
                    {
                        await _shelfService.AddPlantToShelfAsync(firstShelf.Shelf.Id, legacyPlant.Id);
                        int nextPos = firstShelf.Items.Count;
                        firstShelf.Items.Add(new ShelfItemViewModel(legacyPlant, nextPos));
                        plantsOnShelvesIds.Add(legacyPlant.Id);
                    }
                }
            }

            Shelves = new ObservableCollection<ShelfViewModel>(shelfViewModels);
            Books = new ObservableCollection<Book>(allBooks);

            // Available = not on any shelf.
            AvailablePlants = new ObservableCollection<UserPlant>(
                allPlants.Where(p => p.Status != PlantStatus.Dead && !plantsOnShelvesIds.Contains(p.Id))
            );

            var allDecorations = await _decorationService.GetAllAsync();
            AvailableDecorations = new ObservableCollection<UserDecoration>(
                allDecorations.Where(d => !decorationsOnShelvesIds.Contains(d.Id))
            );

            await CalculateGoalStatsAsync();
        }, Tr("Error_FailedTo_LoadBooks"));
    }

    public Task<UserPlant?> GetPlantByIdAsync(Guid plantId)
    {
        return GetPlantByIdInternalAsync(plantId);
    }

    private async Task<UserPlant?> GetPlantByIdInternalAsync(Guid plantId)
    {
        try
        {
            return await _plantService.GetByIdAsync(plantId);
        }
        catch (Exception ex)
        {
            SetError(Tr("Error_LoadPlantDetailsFailed", ex.Message));
            return null;
        }
    }

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
        }, Tr("Error_FailedTo_CreateShelf"));
    }

    [RelayCommand]
    public async Task DeleteShelfAsync(Guid shelfId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.DeleteShelfAsync(shelfId);
            await LoadAsync();
        }, Tr("Error_FailedTo_DeleteShelf"));
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

                Shelves.Move(shelfIndex, shelfIndex - 1);

                var newOrderIds = Shelves.Select(s => s.Shelf.Id).ToList();
                await _shelfService.ReorderShelvesAsync(newOrderIds);
            }
        }, Tr("Error_FailedTo_MoveShelfUp"));
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

                Shelves.Move(shelfIndex, shelfIndex + 1);

                var newOrderIds = Shelves.Select(s => s.Shelf.Id).ToList();
                await _shelfService.ReorderShelvesAsync(newOrderIds);
            }
        }, Tr("Error_FailedTo_MoveShelfDown"));
    }

    [RelayCommand]
    public async Task MoveBookToShelfAsync((Guid bookId, Guid targetShelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            // A book may live on multiple shelves, so drag-and-drop adds rather than moves.
            await _shelfService.AddBookToShelfAsync(args.targetShelfId, args.bookId);
            await LoadAsync();
        }, Tr("Error_FailedTo_MoveBookToShelf"));
    }

    [RelayCommand]
    public async Task RemoveBookFromShelfAsync((Guid bookId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.RemoveBookFromShelfAsync(args.shelfId, args.bookId);
            await LoadAsync();
        }, Tr("Error_FailedTo_RemoveBookFromShelf"));
    }

    private async Task CalculateGoalStatsAsync()
    {
        TbrCount = Books.Count(b => b.Status == ReadingStatus.Planned);

        // Goals and DateCompleted are stored in UTC.
        int currentYear = DateTime.UtcNow.Year;

        // goal.Current already accounts for excluded books, genre filters, and date ranges.
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
        // Search acts on the global book list and hides standard shelves while active.
        await ExecuteSafelyAsync(async () =>
        {
            IEnumerable<Book> filtered;

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                // Only reset to shelf view when nothing to filter; an active status/genre filter falls through to the pipeline below.
                if (!FilterStatus.HasValue && !FilterGenreId.HasValue)
                {
                    await LoadAsync();
                    return;
                }

                filtered = await _bookService.GetAllAsync();
            }
            else
            {
                filtered = await _bookService.SearchAsync(SearchQuery);
            }

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

            filtered = SortBy switch
            {
                "Author" => filtered.OrderBy(b => b.Author),
                "DateAdded" => filtered.OrderByDescending(b => b.DateAdded),
                "Status" => filtered.OrderBy(b => b.Status),
                _ => filtered.OrderBy(b => b.Title)
            };

            Books = new ObservableCollection<Book>(filtered);
            // Clearing shelves signals search mode.
            Shelves.Clear();

        }, Tr("Error_FailedTo_SearchBooks"));
    }

    [RelayCommand]
    public async Task DeleteBookAsync(Guid bookId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _bookService.DeleteAsync(bookId);
            await LoadAsync();
        }, Tr("Error_FailedTo_DeleteBook"));
    }

    [RelayCommand]
    public async Task ClearFilters()
    {
        SearchQuery = "";
        FilterStatus = null;
        FilterGenreId = null;
        await LoadAsync();
    }

    [RelayCommand]
    public async Task AddPlantToShelfAsync((Guid plantId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var plant = AvailablePlants.FirstOrDefault(p => p.Id == args.plantId);

            await _shelfService.AddPlantToShelfAsync(args.shelfId, args.plantId);
            await LoadAsync();
        }, Tr("Error_FailedTo_PlacePlant"));
    }

    [RelayCommand]
    public async Task RemovePlantFromShelfAsync((Guid plantId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.RemovePlantFromShelfAsync(args.shelfId, args.plantId);
            await LoadAsync();
        }, Tr("Error_FailedTo_RemovePlant"));
    }

    [RelayCommand]
    public async Task RenamePlantAsync((Guid plantId, string newName) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var trimmedName = args.newName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                SetError(Tr("PlantDetail_Error_NameEmpty"));
                return;
            }

            if (trimmedName.Length > 100)
            {
                SetError(Tr("PlantDetail_Error_NameTooLong"));
                return;
            }

            var plant = await _plantService.GetByIdAsync(args.plantId);
            if (plant == null)
            {
                SetError(Tr("Error_PlantNotFound"));
                return;
            }

            if (string.Equals(plant.Name, trimmedName, StringComparison.Ordinal))
            {
                return;
            }

            plant.Name = trimmedName;
            await _plantService.UpdateAsync(plant);
            await LoadAsync();
        }, Tr("Error_FailedTo_RenamePlant"));
    }

    [RelayCommand]
    public async Task WaterPlantAsync(Guid plantId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.WaterPlantAsync(plantId);
            await LoadAsync();
        }, Tr("Error_FailedTo_WaterPlant"));
    }

    [RelayCommand]
    public async Task DeletePlantAsync(Guid plantId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.DeleteAsync(plantId);
            await LoadAsync();
        }, Tr("Error_FailedTo_DeletePlant"));
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

            shelfVM.Items.Move(oldIndex, newIndex);

            var bookPositions = new Dictionary<Guid, int>();
            var plantPositions = new Dictionary<Guid, int>();
            var decorationPositions = new Dictionary<Guid, int>();

            for (int i = 0; i < shelfVM.Items.Count; i++)
            {
                var item = shelfVM.Items[i];
                item.Position = i;

                if (item.Type == ShelfItemType.Book)
                    bookPositions[item.Id] = i;
                else if (item.Type == ShelfItemType.Plant)
                    plantPositions[item.Id] = i;
                else if (item.Type == ShelfItemType.Decoration)
                    decorationPositions[item.Id] = i;
            }

            await _shelfService.UpdateShelfPositionsAsync(args.shelfId, bookPositions, plantPositions, decorationPositions);

        }, Tr("Error_FailedTo_ReorderItems"));
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
            else if (args.type == ShelfItemType.Plant)
                await _shelfService.MovePlantBetweenShelvesAsync(
                    args.sourceShelfId, args.targetShelfId, args.itemId, args.position);
            else if (args.type == ShelfItemType.Decoration)
                await _shelfService.MoveDecorationBetweenShelvesAsync(
                    args.sourceShelfId, args.targetShelfId, args.itemId, args.position);

            await LoadAsync();
        }, Tr("Error_FailedTo_MoveItemBetweenShelves"));
    }

    /// <summary>Optimistic in-memory move for instant drag-and-drop feedback; does NOT persist — call PersistCrossShelfMoveAsync after.</summary>
    public void MoveItemBetweenShelvesInMemory(
        Guid sourceShelfId, Guid targetShelfId, Guid itemId, ShelfItemType type, int position)
    {
        var sourceShelf = Shelves.FirstOrDefault(s => s.Shelf.Id == sourceShelfId);
        var targetShelf = Shelves.FirstOrDefault(s => s.Shelf.Id == targetShelfId);
        if (sourceShelf == null || targetShelf == null) return;

        var item = sourceShelf.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return;

        sourceShelf.Items.Remove(item);
        for (int i = 0; i < sourceShelf.Items.Count; i++)
            sourceShelf.Items[i].Position = i;

        int insertAt = position >= 0 && position <= targetShelf.Items.Count
            ? position
            : targetShelf.Items.Count;

        targetShelf.Items.Insert(insertAt, item);
        for (int i = 0; i < targetShelf.Items.Count; i++)
            targetShelf.Items[i].Position = i;
    }

    /// <summary>Persists a cross-shelf move without reloading all shelves.</summary>
    public async Task PersistCrossShelfMoveAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid itemId, ShelfItemType type, int position)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (type == ShelfItemType.Book)
                await _shelfService.MoveBookBetweenShelvesAsync(
                    sourceShelfId, targetShelfId, itemId, position);
            else if (type == ShelfItemType.Plant)
                await _shelfService.MovePlantBetweenShelvesAsync(
                    sourceShelfId, targetShelfId, itemId, position);
            else if (type == ShelfItemType.Decoration)
                await _shelfService.MoveDecorationBetweenShelvesAsync(
                    sourceShelfId, targetShelfId, itemId, position);
        }, Tr("Error_FailedTo_PersistShelfMove"));
    }

    [RelayCommand]
    public async Task AddDecorationToShelfAsync((Guid decorationId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.AddDecorationToShelfAsync(args.shelfId, args.decorationId);
            await LoadAsync();
        }, Tr("Error_FailedTo_PlaceDecoration"));
    }

    [RelayCommand]
    public async Task RemoveDecorationFromShelfAsync((Guid decorationId, Guid shelfId) args)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _shelfService.RemoveDecorationFromShelfAsync(args.shelfId, args.decorationId);
            await LoadAsync();
        }, Tr("Error_FailedTo_RemoveDecoration"));
    }

    [RelayCommand]
    public async Task DeleteDecorationAsync(Guid decorationId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _decorationService.DeleteAsync(decorationId);
            await LoadAsync();
        }, Tr("Error_FailedTo_DeleteDecoration"));
    }

    /// <summary>Refreshes only the goal header stats without reloading shelf data.</summary>
    public async Task RefreshGoalStatsAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            await CalculateGoalStatsAsync();
        }, Tr("Error_FailedTo_RefreshGoalStats"));
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
