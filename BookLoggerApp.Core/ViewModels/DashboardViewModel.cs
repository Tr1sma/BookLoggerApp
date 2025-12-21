using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IBookService _bookService;
    private readonly IProgressService _progressService;
    private readonly IGoalService _goalService;
    private readonly IPlantService _plantService;
    private readonly IStatsService _statsService;

    public DashboardViewModel(
        IBookService bookService,
        IProgressService progressService,
        IGoalService goalService,
        IPlantService plantService,
        IStatsService statsService)
    {
        _bookService = bookService;
        _progressService = progressService;
        _goalService = goalService;
        _plantService = plantService;
        _statsService = statsService;
    }

    [ObservableProperty]
    private Book? _currentlyReading;

    [ObservableProperty]
    private int _booksReadThisWeek;

    [ObservableProperty]
    private int _minutesReadThisWeek;

    [ObservableProperty]
    private int _pagesReadThisWeek;

    [ObservableProperty]
    private int _xpEarnedThisWeek;

    [ObservableProperty]
    private List<ReadingGoal> _activeGoals = new();

    [ObservableProperty]
    private UserPlant? _activePlant;

    [ObservableProperty]
    private List<ReadingSession> _recentActivity = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            // Currently Reading Book
            CurrentlyReading = await _bookService.GetCurrentlyReadingBookAsync();

            // This Week Stats
            var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
            var weekEnd = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            // Calculate books read this week (completed books)
            BooksReadThisWeek = await _bookService.GetCompletedCountByDateRangeAsync(weekStart, weekEnd);

            // Get sessions this week
            var weekSessions = await _progressService.GetSessionsInRangeAsync(weekStart, weekEnd);
            MinutesReadThisWeek = weekSessions.Sum(s => s.Minutes);
            PagesReadThisWeek = weekSessions.Sum(s => s.PagesRead ?? 0);
            XpEarnedThisWeek = weekSessions.Sum(s => s.XpEarned);

            // Active Goals
            var goals = await _goalService.GetActiveGoalsAsync();
            ActiveGoals = goals.ToList();

            // Active Plant
            ActivePlant = await _plantService.GetActivePlantAsync();

            // Recent Activity
            var recentSessions = await _progressService.GetRecentSessionsAsync(5);
            RecentActivity = recentSessions.ToList();
        }, "Failed to load dashboard");
    }

    [RelayCommand]
    public async Task WaterPlantAsync()
    {
        if (ActivePlant == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.WaterPlantAsync(ActivePlant.Id);
            // Reload plant after watering
            ActivePlant = await _plantService.GetActivePlantAsync();
        }, "Failed to water plant");
    }
}

