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

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
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
        ShowCreateForm = true;
        NewGoal = new ReadingGoal
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1),
            Type = GoalType.Books,
            Target = 1,
            Current = 0
        };
    }

    [RelayCommand]
    public void CancelCreate()
    {
        ShowCreateForm = false;
        NewGoal = null;
    }

    [RelayCommand]
    public async Task CreateGoalAsync()
    {
        if (NewGoal == null) return;

        if (string.IsNullOrWhiteSpace(NewGoal.Title))
        {
            SetError("Goal title is required");
            return;
        }

        await ExecuteSafelyAsync(async () =>
        {
            await _goalService.AddAsync(NewGoal);
            ShowCreateForm = false;
            NewGoal = null;
            await LoadAsync();
        }, "Failed to create goal");
    }

    [RelayCommand]
    public async Task DeleteGoalAsync(Guid goalId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _goalService.DeleteAsync(goalId);
            await LoadAsync();
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

