using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Helpers;
using System.Collections.ObjectModel;

namespace BookLoggerApp.Core.ViewModels;

public partial class StatsViewModel : ViewModelBase
{
    private readonly IStatsService _statsService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IPlantService _plantService;

    public StatsViewModel(
        IStatsService statsService,
        IAppSettingsProvider settingsProvider,
        IPlantService plantService)
    {
        _statsService = statsService;
        _settingsProvider = settingsProvider;
        _plantService = plantService;
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
    private Dictionary<RatingCategory, double> _categoryAverages = new();

    [ObservableProperty]
    private ObservableCollection<BookRatingSummary> _topRatedBooks = new();

    // === Progression System Properties ===

    [ObservableProperty]
    private int _currentLevel = 1;

    [ObservableProperty]
    private int _totalXp = 0;

    [ObservableProperty]
    private int _currentLevelXp = 0; // XP accumulated in current level

    [ObservableProperty]
    private int _nextLevelXp = 100; // XP needed for next level

    [ObservableProperty]
    private decimal _progressPercentage = 0m; // 0-100

    [ObservableProperty]
    private int _totalCoins = 0;

    [ObservableProperty]
    private decimal _totalPlantBoost = 0m; // Total XP boost from all plants (e.g., 0.15 = 15%)

    [ObservableProperty]
    private ObservableCollection<PlantBoostInfo> _plantBoosts = new();

    [ObservableProperty]
    private ObservableCollection<LevelMilestone> _levelMilestones = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
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

            // Load progression statistics
            await LoadProgressionStatisticsAsync();
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
        System.Diagnostics.Debug.WriteLine($"FilterTopBooksByCategoryAsync called with category: {category}");

        try
        {
            var topBooks = await _statsService.GetTopRatedBooksAsync(10, category);
            System.Diagnostics.Debug.WriteLine($"Got {topBooks.Count} books from service");

            TopRatedBooks = new ObservableCollection<BookRatingSummary>(topBooks);
            System.Diagnostics.Debug.WriteLine($"TopRatedBooks updated, count: {TopRatedBooks.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in FilterTopBooksByCategoryAsync: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Loads progression system statistics (level, XP, coins, plant boosts).
    /// </summary>
    private async Task LoadProgressionStatisticsAsync()
    {
        var settings = await _settingsProvider.GetSettingsAsync();

        CurrentLevel = settings.UserLevel;
        TotalXp = settings.TotalXp;
        TotalCoins = settings.Coins;

        // Recalculate level from total XP to ensure consistency and fix sync bugs
        CurrentLevel = XpCalculator.CalculateLevelFromXp(TotalXp);

        // Calculate XP for current level progress
        CalculateProgress();

        // Load plant boost details
        TotalPlantBoost = await _plantService.CalculateTotalXpBoostAsync();

        var plants = await _plantService.GetAllAsync();
        var plantBoostList = new List<PlantBoostInfo>();

        foreach (var plant in plants)
        {
            var baseBoost = plant.Species.XpBoostPercentage;
            var levelBonus = plant.CurrentLevel * (plant.Species.XpBoostPercentage / plant.Species.MaxLevel);
            var totalBoost = baseBoost + levelBonus;

            plantBoostList.Add(new PlantBoostInfo
            {
                PlantId = plant.Id,
                PlantName = plant.Species.Name,
                PlantLevel = plant.CurrentLevel,
                BoostPercentage = totalBoost
            });
        }

        PlantBoosts = new ObservableCollection<PlantBoostInfo>(plantBoostList);

        // Generate level milestones (show -2, current, +5 levels)
        GenerateLevelMilestones();
    }

    private void CalculateProgress()
    {
        // Use uniform logic from XpCalculator
        
        // Calculate XP accumulated for current level
        int xpForPreviousLevels = 0;
        for (int i = 1; i < CurrentLevel; i++)
        {
            xpForPreviousLevels += BookLoggerApp.Core.Helpers.XpCalculator.GetXpForLevel(i);
        }

        CurrentLevelXp = TotalXp - xpForPreviousLevels;
        NextLevelXp = BookLoggerApp.Core.Helpers.XpCalculator.GetXpForLevel(CurrentLevel);

        // Calculate percentage (0-100)
        if (NextLevelXp > 0)
        {
            ProgressPercentage = (decimal)CurrentLevelXp / NextLevelXp * 100m;
        }
        else
        {
            ProgressPercentage = 0m;
        }
    }

    /// <summary>
    /// Calculate XP required for a specific level (matches XpCalculator logic).
    /// </summary>
    private static int GetXpForLevel(int level)
    {
        return BookLoggerApp.Core.Helpers.XpCalculator.GetXpForLevel(level);
    }

    private void GenerateLevelMilestones()
    {
        var milestones = new List<LevelMilestone>();

        // Show past levels (if any)
        int startLevel = Math.Max(1, CurrentLevel - 2);

        // Show up to +5 future levels
        int endLevel = CurrentLevel + 5;

        // Calculate cumulative XP
        int cumulativeXp = 0;
        for (int level = 1; level < startLevel; level++)
        {
            cumulativeXp += GetXpForLevel(level);
        }

        for (int level = startLevel; level <= endLevel; level++)
        {
            int xpForThisLevel = GetXpForLevel(level);
            cumulativeXp += xpForThisLevel;

            milestones.Add(new LevelMilestone
            {
                Level = level,
                XpRequired = cumulativeXp,
                CoinsReward = level * 50,
                IsCompleted = level < CurrentLevel,
                IsCurrent = level == CurrentLevel
            });
        }

        LevelMilestones = new ObservableCollection<LevelMilestone>(milestones);
    }
}

