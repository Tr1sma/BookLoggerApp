using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.ViewModels;

public partial class GoalsViewModel : ViewModelBase
{
    private readonly IGoalService _goalService;
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;

    public GoalsViewModel(IGoalService goalService, IBookService bookService, IGenreService genreService)
    {
        _goalService = goalService;
        _bookService = bookService;
        _genreService = genreService;
    }

    [ObservableProperty]
    private List<ReadingGoal> _activeGoals = new();

    [ObservableProperty]
    private List<ReadingGoal> _completedGoals = new();

    [ObservableProperty]
    private ReadingGoal? _newGoal;

    [ObservableProperty]
    private bool _showCreateForm = false;

    [ObservableProperty]
    private bool _isEditing = false;

    [ObservableProperty]
    private string? _statusMessage;

    // Exclusion modal state
    [ObservableProperty]
    private bool _showExcludeModal = false;

    [ObservableProperty]
    private ReadingGoal? _excludeModalGoal;

    [ObservableProperty]
    private List<Book> _allBooks = new();

    [ObservableProperty]
    private HashSet<Guid> _excludedBookIds = new();

    // Genre filter state
    [ObservableProperty]
    private List<Genre> _allGenres = new();

    [ObservableProperty]
    private HashSet<Guid> _selectedGenreIds = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            var genres = await _genreService.GetAllAsync();
            AllGenres = genres.OrderBy(g => g.Name).ToList();

            var active = await _goalService.GetActiveGoalsAsync();
            ActiveGoals = active.ToList();

            var completed = await _goalService.GetCompletedGoalsAsync();
            CompletedGoals = completed.ToList();
        }, "Failed to load goals");
    }

    [RelayCommand]
    public void OpenCreateForm()
    {
        StatusMessage = null;
        ShowCreateForm = true;
        IsEditing = false;
        SelectedGenreIds = new();
        // For Books goals, default to yearly tracking (Jan 1 - Dec 31)
        // For other goals, use current date as start
        var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1);
        var endOfYear = new DateTime(DateTime.UtcNow.Year, 12, 31);

        NewGoal = new ReadingGoal
        {
            // Start from beginning of year to include all books completed this year
            StartDate = startOfYear,
            EndDate = endOfYear,
            Type = GoalType.Books,
            Target = 1,
            Current = 0
        };
    }

    [RelayCommand]
    public void OpenEditForm(ReadingGoal goal)
    {
        StatusMessage = null;
        // Create a copy to edit to avoid modifying the list directly before saving
        NewGoal = new ReadingGoal
        {
            Id = goal.Id,
            Title = goal.Title,
            Description = goal.Description,
            Type = goal.Type,
            Target = goal.Target,
            Current = goal.Current,
            StartDate = goal.StartDate,
            EndDate = goal.EndDate,
            IsCompleted = goal.IsCompleted,
            CompletedAt = goal.CompletedAt,
            RowVersion = goal.RowVersion
        };
        IsEditing = true;
        ShowCreateForm = true;
    }

    [RelayCommand]
    public void CancelCreate()
    {
        ShowCreateForm = false;
        NewGoal = null;
        IsEditing = false;
        StatusMessage = null;
    }

    [RelayCommand]
    public async Task SaveGoalAsync()
    {
        if (NewGoal == null) return;

        if (string.IsNullOrWhiteSpace(NewGoal.Title))
        {
            SetError("Goal title is required");
            return;
        }

        await ExecuteSafelyAsync(async () =>
        {
            bool wasEditing = IsEditing;
            if (IsEditing)
            {
                await _goalService.UpdateAsync(NewGoal);
                StatusMessage = "Update erfolgreich";
            }
            else
            {
                var created = await _goalService.AddAsync(NewGoal);
                // Persist selected genres for the newly created goal
                foreach (var genreId in SelectedGenreIds)
                {
                    await _goalService.AddGenreToGoalAsync(created.Id, genreId);
                }
                StatusMessage = "Ziel erstellt";
            }
            
            ShowCreateForm = false;
            NewGoal = null;
            IsEditing = false;
            await LoadAsync();

            // Clear message after 3 seconds
            _ = Task.Delay(3000).ContinueWith(_ => StatusMessage = null);

        }, IsEditing ? "Failed to update goal" : "Failed to create goal");
    }

    [RelayCommand]
    public async Task DeleteGoalAsync(Guid goalId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _goalService.DeleteAsync(goalId);
            if (ShowCreateForm && NewGoal?.Id == goalId)
            {
                ShowCreateForm = false;
                NewGoal = null;
            }
            StatusMessage = "Erfolgreich gelöscht";
            await LoadAsync();
            
            // Clear message after 3 seconds
            _ = Task.Delay(3000).ContinueWith(_ => StatusMessage = null);

        }, "Failed to delete goal");
    }

    [RelayCommand]
    public async Task UpdateGoalAsync(ReadingGoal goal)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _goalService.UpdateAsync(goal);
            await LoadAsync();
        }, "Failed to update goal");
    }

    [RelayCommand]
    public async Task OpenExcludeModalAsync(ReadingGoal goal)
    {
        await ExecuteSafelyAsync(async () =>
        {
            ExcludeModalGoal = goal;

            // Set up editing (same copy pattern as OpenEditForm)
            NewGoal = new ReadingGoal
            {
                Id = goal.Id,
                Title = goal.Title,
                Description = goal.Description,
                Type = goal.Type,
                Target = goal.Target,
                Current = goal.Current,
                StartDate = goal.StartDate,
                EndDate = goal.EndDate,
                IsCompleted = goal.IsCompleted,
                CompletedAt = goal.CompletedAt,
                RowVersion = goal.RowVersion
            };
            IsEditing = true;

            // Load all books
            var books = await _bookService.GetAllAsync();
            AllBooks = books.OrderBy(b => b.Title).ToList();

            // Load current exclusions for this goal
            var exclusions = await _goalService.GetExcludedBooksAsync(goal.Id);
            ExcludedBookIds = exclusions.Select(e => e.BookId).ToHashSet();

            // Load current genre filter for this goal
            var goalGenres = await _goalService.GetGoalGenresAsync(goal.Id);
            SelectedGenreIds = goalGenres.Select(gg => gg.GenreId).ToHashSet();

            ShowExcludeModal = true;
        }, "Fehler beim Laden der Bücher");
    }

    [RelayCommand]
    public async Task ToggleGoalGenreAsync(Guid genreId)
    {
        if (ExcludeModalGoal == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            if (SelectedGenreIds.Contains(genreId))
            {
                await _goalService.RemoveGenreFromGoalAsync(ExcludeModalGoal.Id, genreId);
                SelectedGenreIds.Remove(genreId);
            }
            else
            {
                await _goalService.AddGenreToGoalAsync(ExcludeModalGoal.Id, genreId);
                SelectedGenreIds.Add(genreId);
            }

            // Force UI update by replacing the set
            SelectedGenreIds = new HashSet<Guid>(SelectedGenreIds);
        }, "Fehler beim Ändern des Genre-Filters");
    }

    [RelayCommand]
    public async Task ToggleBookExclusionAsync(Guid bookId)
    {
        if (ExcludeModalGoal == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            if (ExcludedBookIds.Contains(bookId))
            {
                await _goalService.IncludeBookInGoalAsync(ExcludeModalGoal.Id, bookId);
                ExcludedBookIds.Remove(bookId);
            }
            else
            {
                await _goalService.ExcludeBookFromGoalAsync(ExcludeModalGoal.Id, bookId);
                ExcludedBookIds.Add(bookId);
            }

            // Force UI update by replacing the set
            ExcludedBookIds = new HashSet<Guid>(ExcludedBookIds);
        }, "Fehler beim Ändern der Ausschließung");
    }

    [RelayCommand]
    public async Task CloseExcludeModalAsync()
    {
        ShowExcludeModal = false;
        ExcludeModalGoal = null;
        AllBooks = new();
        ExcludedBookIds = new();
        SelectedGenreIds = new();
        NewGoal = null;
        IsEditing = false;

        // Reload goals to reflect updated progress
        await LoadAsync();
    }
}

