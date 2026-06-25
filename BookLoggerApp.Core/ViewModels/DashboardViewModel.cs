using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Helpers;
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
    private readonly IReadingForecastService _readingForecastService;

    public DashboardViewModel(
        IBookService bookService,
        IProgressService progressService,
        IGoalService goalService,
        IPlantService plantService,
        IStatsService statsService,
        IReadingForecastService readingForecastService)
    {
        _bookService = bookService;
        _progressService = progressService;
        _goalService = goalService;
        _plantService = plantService;
        _statsService = statsService;
        _readingForecastService = readingForecastService;
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

    /// <summary>Currently-reading books with a finish forecast, soonest first; empty when none can be forecast.</summary>
    [ObservableProperty]
    private List<UpcomingFinish> _upcomingFinishes = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            await _plantService.UpdatePlantStatusesAsync(ct);

            var readingBooks = await _bookService.GetByStatusAsync(ReadingStatus.Reading, ct);
            CurrentlyReading = readingBooks.FirstOrDefault();

            // ISO 8601 week starts Monday, local calendar (consistent with Goals/Streaks); stats timestamps are canonical UTC.
            var timeZone = TimeZoneInfo.Local;
            var today = DateTime.Now.Date;
            int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var weekStart = today.AddDays(-daysSinceMonday);
            var weekEnd = today.AddDays(1).AddTicks(-1);

            // Bucket UTC DateCompleted by local date.
            var completedBooksThisWeek = await _bookService.GetByStatusAsync(ReadingStatus.Completed, ct);
            BooksReadThisWeek = completedBooksThisWeek.Count(b => b.DateCompleted.HasValue &&
                LocalTimeHelper.ToLocal(b.DateCompleted.Value, timeZone) is var localCompleted &&
                localCompleted >= weekStart && localCompleted <= weekEnd);

            // Convert local week bounds to UTC for the StartedAt query.
            var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(weekStart, timeZone);
            var weekEndUtc = TimeZoneInfo.ConvertTimeToUtc(weekEnd, timeZone);
            var weekSessions = await _progressService.GetSessionsInRangeAsync(weekStartUtc, weekEndUtc, ct);
            MinutesReadThisWeek = weekSessions.Sum(s => s.Minutes);
            PagesReadThisWeek = weekSessions.Sum(s => s.PagesRead ?? 0);
            XpEarnedThisWeek = weekSessions.Sum(s => s.XpEarned);

            var goals = await _goalService.GetActiveGoalsAsync(ct);
            ActiveGoals = goals.ToList();

            ActivePlant = await _plantService.GetActivePlantAsync(ct);

            var recentSessions = await _progressService.GetRecentSessionsAsync(5, ct);
            RecentActivity = recentSessions.ToList();

            UpcomingFinishes = (await _readingForecastService.GetUpcomingFinishesAsync(ct)).ToList();
        }, Tr("Error_FailedTo_LoadDashboard"));
    }

    [RelayCommand]
    public async Task WaterPlantAsync()
    {
        if (ActivePlant == null) return;

        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.WaterPlantAsync(ActivePlant.Id);
            ActivePlant = await _plantService.GetActivePlantAsync();
        }, Tr("Error_FailedTo_WaterPlant"));
    }

    [RelayCommand]
    public async Task DeletePlantAsync()
    {
        if (ActivePlant == null) return;

        var plantId = ActivePlant.Id;

        await ExecuteSafelyAsync(async () =>
        {
            await _plantService.DeleteAsync(plantId);
            ActivePlant = await _plantService.GetActivePlantAsync();
        }, Tr("Error_FailedTo_DeletePlant"));
    }
}

