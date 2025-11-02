using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class StatsViewModel : ViewModelBase
{
    private readonly IStatsService _statsService;

    public StatsViewModel(IStatsService statsService)
    {
        _statsService = statsService;
    }

    [ObservableProperty]
    private int _totalBooksRead;

    [ObservableProperty]
    private int _totalPagesRead;

    [ObservableProperty]
    private int _totalMinutesRead;

    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private int _longestStreak;

    [ObservableProperty]
    private double _averageRating;

    [ObservableProperty]
    private Dictionary<DateTime, int> _readingTrend = new();

    [ObservableProperty]
    private Dictionary<string, int> _booksByGenre = new();

    [ObservableProperty]
    private string? _favoriteGenre;

    [ObservableProperty]
    private DateTime _dateRangeStart = DateTime.UtcNow.AddMonths(-1);

    [ObservableProperty]
    private DateTime _dateRangeEnd = DateTime.UtcNow;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            TotalBooksRead = await _statsService.GetTotalBooksReadAsync();
            TotalPagesRead = await _statsService.GetTotalPagesReadAsync();
            TotalMinutesRead = await _statsService.GetTotalMinutesReadAsync();
            CurrentStreak = await _statsService.GetCurrentStreakAsync();
            LongestStreak = await _statsService.GetLongestStreakAsync();
            AverageRating = await _statsService.GetAverageRatingAsync();

            ReadingTrend = await _statsService.GetReadingTrendAsync(DateRangeStart, DateRangeEnd);
            BooksByGenre = await _statsService.GetBooksByGenreAsync();
            FavoriteGenre = await _statsService.GetFavoriteGenreAsync();
        }, "Failed to load statistics");
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync();
    }
}

