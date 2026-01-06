using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.ViewModels;

public partial class GoalsViewModel : ViewModelBase
{
    private readonly IGoalService _goalService;

    public GoalsViewModel(IGoalService goalService)
    {
        _goalService = goalService;
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

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
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
                await _goalService.AddAsync(NewGoal);
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
}

