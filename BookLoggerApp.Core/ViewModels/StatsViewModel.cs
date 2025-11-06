using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Models;
using System.Collections.ObjectModel;

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

    // Multi-Category Rating Statistics
    [ObservableProperty]
    private double _averageCharactersRating;

    [ObservableProperty]
    private double _averagePlotRating;

    [ObservableProperty]
    private double _averageWritingStyleRating;

    [ObservableProperty]
    private double _averageSpiceLevelRating;

    [ObservableProperty]
    private double _averagePacingRating;

    [ObservableProperty]
    private double _averageWorldBuildingRating;

    [ObservableProperty]
    private double _averageOverallRating;

    [ObservableProperty]
    private Dictionary<RatingCategory, double> _categoryAverages = new();

    [ObservableProperty]
    private ObservableCollection<BookRatingSummary> _topRatedBooks = new();

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

            // Load rating statistics
            await LoadRatingStatisticsAsync();
        }, "Failed to load statistics");
    }

    /// <summary>
    /// Loads multi-category rating statistics.
    /// </summary>
    private async Task LoadRatingStatisticsAsync()
    {
        CategoryAverages = await _statsService.GetAllAverageRatingsAsync(DateRangeStart, DateRangeEnd);

        // Set individual category averages
        AverageCharactersRating = CategoryAverages.GetValueOrDefault(RatingCategory.Characters, 0);
        AveragePlotRating = CategoryAverages.GetValueOrDefault(RatingCategory.Plot, 0);
        AverageWritingStyleRating = CategoryAverages.GetValueOrDefault(RatingCategory.WritingStyle, 0);
        AverageSpiceLevelRating = CategoryAverages.GetValueOrDefault(RatingCategory.SpiceLevel, 0);
        AveragePacingRating = CategoryAverages.GetValueOrDefault(RatingCategory.Pacing, 0);
        AverageWorldBuildingRating = CategoryAverages.GetValueOrDefault(RatingCategory.WorldBuilding, 0);
        AverageOverallRating = CategoryAverages.GetValueOrDefault(RatingCategory.Overall, 0);

        // Load top rated books
        var topBooks = await _statsService.GetTopRatedBooksAsync(10);
        TopRatedBooks = new ObservableCollection<BookRatingSummary>(topBooks);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync();
    }

    /// <summary>
    /// Filters top rated books by a specific rating category.
    /// </summary>
    public async Task FilterTopBooksByCategoryAsync(RatingCategory? category = null)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var topBooks = await _statsService.GetTopRatedBooksAsync(10, category);
            TopRatedBooks = new ObservableCollection<BookRatingSummary>(topBooks);
        }, "Failed to filter top books");
    }
}

