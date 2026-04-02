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
    private readonly IShareCardService _shareCardService;
    private readonly IProgressService _progressService;
    private readonly IBookService _bookService;

    /// <summary>
    /// Raised when a stats share card PNG is ready. The component handles file write + sharing.
    /// </summary>
    public event Action<byte[]>? ShareCardReady;

    public StatsViewModel(
        IStatsService statsService,
        IAppSettingsProvider settingsProvider,
        IPlantService plantService,
        IShareCardService shareCardService,
        IProgressService progressService,
        IBookService bookService)
    {
        _statsService = statsService;
        _settingsProvider = settingsProvider;
        _plantService = plantService;
        _shareCardService = shareCardService;
        _progressService = progressService;
        _bookService = bookService;
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

    // === Share Card Properties ===

    [ObservableProperty]
    private string _selectedSharePeriod = DateTime.UtcNow.ToString("yyyy-MM");

    [ObservableProperty]
    private bool _showShareModal;

    [ObservableProperty]
    private bool _isGeneratingCard;

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
        await ExecuteSafelyAsync(async () =>
        {
            var topBooks = await _statsService.GetTopRatedBooksAsync(10, category);
            TopRatedBooks = new ObservableCollection<BookRatingSummary>(topBooks);
        }, "Failed to filter top books");
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
            if (plant.Species == null)
                continue;

            var baseBoost = plant.Species.XpBoostPercentage;
            var levelBonus = plant.Species.MaxLevel > 0
                ? plant.CurrentLevel * (plant.Species.XpBoostPercentage / plant.Species.MaxLevel)
                : 0m;
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

        // Calculate percentage (0-100), clamped to valid range
        if (NextLevelXp > 0)
        {
            ProgressPercentage = Math.Clamp((decimal)CurrentLevelXp / NextLevelXp * 100m, 0m, 100m);
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

    [RelayCommand]
    public async Task GenerateAndShareStatsCardAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            IsGeneratingCard = true;

            var (start, end) = GetShareDateRange(SelectedSharePeriod);
            bool isAllTime = SelectedSharePeriod == "All Time";

            int books = isAllTime
                ? await _statsService.GetTotalBooksReadAsync()
                : await _statsService.GetBooksCompletedInRangeAsync(start, end);

            int pages = isAllTime
                ? await _statsService.GetTotalPagesReadAsync()
                : await _statsService.GetPagesReadInRangeAsync(start, end);

            int minutes;
            if (isAllTime)
            {
                minutes = await _progressService.GetTotalMinutesAllBooksAsync();
            }
            else
            {
                var trend = await _statsService.GetReadingTrendAsync(start, end);
                minutes = trend.Values.Sum();
            }

            string? genre = await _statsService.GetFavoriteGenreAsync();
            double avgRating = await _statsService.GetAverageRatingAsync();
            int totalBooks = await _bookService.GetTotalCountAsync();

            var topBooks = await _statsService.GetTopBooksInRangeAsync(
                isAllTime ? new DateTime(2000, 1, 1) : start,
                isAllTime ? DateTime.UtcNow : end,
                3);

            var data = new StatsShareData
            {
                PeriodLabel = GetPeriodDisplayLabel(SelectedSharePeriod),
                BooksCompleted = books,
                PagesRead = pages,
                MinutesRead = minutes,
                FavoriteGenre = genre,
                TopBooks = topBooks.Select(b => (b.Title, b.Author, b.AverageRating)).ToList(),
                UserLevel = CurrentLevel,
                AverageRating = avgRating > 0 ? avgRating : null,
                TotalBooks = totalBooks
            };

            byte[] cardBytes = await _shareCardService.GenerateStatsCardAsync(data);
            ShareCardReady?.Invoke(cardBytes);

            ShowShareModal = false;
            IsGeneratingCard = false;
        }, "Failed to generate share card");

        IsGeneratingCard = false;
    }

    private static (DateTime start, DateTime end) GetShareDateRange(string period)
    {
        if (period == "All Time")
            return (new DateTime(2000, 1, 1), DateTime.UtcNow);

        if (period == "Year")
            return (new DateTime(DateTime.UtcNow.Year, 1, 1), DateTime.UtcNow);

        // Specific year format (e.g., "2025", "2024")
        if (period.Length == 4 && int.TryParse(period, out int yearOnly))
        {
            var yearStart = new DateTime(yearOnly, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearEnd = yearOnly == DateTime.UtcNow.Year
                ? DateTime.UtcNow
                : new DateTime(yearOnly, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            return (yearStart, yearEnd);
        }

        // "yyyy-MM" specific month format
        if (period.Length == 7 && period[4] == '-'
            && int.TryParse(period[..4], out int year)
            && int.TryParse(period[5..], out int month))
        {
            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1).AddTicks(-1);
            return (start, end);
        }

        // Fallback: last 30 days
        return (DateTime.UtcNow.Date.AddDays(-29), DateTime.UtcNow);
    }

    private static string GetPeriodDisplayLabel(string period) => period switch
    {
        "All Time" => "All Time",
        "Year" => DateTime.UtcNow.Year.ToString(),
        _ when period.Length == 4 && int.TryParse(period, out _) => period,
        _ when period.Length == 7 && period[4] == '-'
            && int.TryParse(period[..4], out int y)
            && int.TryParse(period[5..], out int m)
            => new DateTime(y, m, 1).ToString("MMMM yyyy"),
        _ => period
    };

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

