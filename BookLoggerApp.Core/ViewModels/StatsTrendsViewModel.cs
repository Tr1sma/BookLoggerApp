using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class StatsTrendsViewModel : ViewModelBase
{
    private readonly IAdvancedStatsService _advancedStatsService;
    private readonly IStatsService _statsService;

    public StatsTrendsViewModel(IAdvancedStatsService advancedStatsService, IStatsService statsService)
    {
        _advancedStatsService = advancedStatsService;
        _statsService = statsService;
        _heatmapYear = DateTime.UtcNow.Year;
        _monthlyVolumeYear = DateTime.UtcNow.Year;
    }

    // Year navigation
    [ObservableProperty]
    private int _minYear;

    [ObservableProperty]
    private int _maxYear;

    // Heatmap
    [ObservableProperty]
    private int _heatmapYear;

    [ObservableProperty]
    private Dictionary<DateTime, int> _heatmapData = new();

    // Weekday distribution
    [ObservableProperty]
    private Dictionary<DayOfWeek, int> _weekdayData = new();

    // Time of day
    [ObservableProperty]
    private Dictionary<string, int> _timeOfDayData = new();

    [ObservableProperty]
    private string _timeOfDayLabel = "";

    // Session lengths
    [ObservableProperty]
    private Dictionary<string, int> _sessionLengthData = new();

    [ObservableProperty]
    private double _averageSessionMinutes;

    // Monthly volume
    [ObservableProperty]
    private int _monthlyVolumeYear;

    [ObservableProperty]
    private Dictionary<int, int> _monthlyVolumeData = new();

    // Reading speed
    [ObservableProperty]
    private double _currentSpeed;

    [ObservableProperty]
    private double _speedDifference;

    // Average finish time
    [ObservableProperty]
    private double _currentFinishDays;

    [ObservableProperty]
    private double _finishDaysDifference;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            var periods = await _statsService.GetActiveReadingPeriodsAsync(ct);
            var years = periods.Select(p => p.Year).Distinct().OrderBy(y => y).ToList();
            MinYear = years.Count > 0 ? years[0] : DateTime.UtcNow.Year;
            MaxYear = DateTime.UtcNow.Year;

            var heatmapTask = _advancedStatsService.GetReadingHeatmapAsync(HeatmapYear, ct);
            var weekdayTask = _advancedStatsService.GetWeekdayDistributionAsync(ct);
            var timeOfDayTask = _advancedStatsService.GetTimeOfDayDistributionAsync(ct);
            var sessionLengthTask = _advancedStatsService.GetSessionLengthDistributionAsync(ct);
            var monthlyTask = _advancedStatsService.GetMonthlyVolumeAsync(MonthlyVolumeYear, ct);
            var speedTask = _advancedStatsService.GetReadingSpeedTrendAsync(ct);
            var finishTask = _advancedStatsService.GetAverageFinishTimeTrendAsync(ct);

            await Task.WhenAll(heatmapTask, weekdayTask, timeOfDayTask, sessionLengthTask, monthlyTask, speedTask, finishTask);

            HeatmapData = heatmapTask.Result;
            WeekdayData = weekdayTask.Result;
            TimeOfDayData = timeOfDayTask.Result;
            TimeOfDayLabel = DetermineTimeOfDayLabel(TimeOfDayData);
            SessionLengthData = sessionLengthTask.Result;
            AverageSessionMinutes = CalculateAverageSession(SessionLengthData);
            MonthlyVolumeData = monthlyTask.Result;

            var (currentSpeed, previousSpeed) = speedTask.Result;
            CurrentSpeed = currentSpeed;
            SpeedDifference = Math.Round(currentSpeed - previousSpeed, 0);

            var (currentFinish, previousFinish) = finishTask.Result;
            CurrentFinishDays = currentFinish;
            FinishDaysDifference = Math.Round(currentFinish - previousFinish, 1);
        }, Tr("Error_FailedTo_LoadTrendStatistics"));
    }

    [RelayCommand]
    public async Task ChangeHeatmapYearAsync(int year)
    {
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            HeatmapYear = year;
            HeatmapData = await _advancedStatsService.GetReadingHeatmapAsync(year, ct);
        }, Tr("Error_FailedTo_LoadTrendStatistics"));
    }

    [RelayCommand]
    public async Task ChangeMonthlyVolumeYearAsync(int year)
    {
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            MonthlyVolumeYear = year;
            MonthlyVolumeData = await _advancedStatsService.GetMonthlyVolumeAsync(year, ct);
        }, Tr("Error_FailedTo_LoadTrendStatistics"));
    }

    private static string DetermineTimeOfDayLabel(Dictionary<string, int> data)
    {
        if (data.Count == 0 || data.Values.All(v => v == 0))
            return "";

        string dominant = data.MaxBy(kv => kv.Value).Key;
        return dominant switch
        {
            "Morning" => "Early Bird 🌅",
            "Afternoon" => "Daydreamer ☀️",
            "Evening" => "Evening Reader 🌙",
            "Night" => "Night Owl 🦉",
            _ => ""
        };
    }

    private static double CalculateAverageSession(Dictionary<string, int> data)
    {
        var bucketMidpoints = new Dictionary<string, double>
        {
            ["<15"] = 7.5, ["15-30"] = 22.5, ["30-60"] = 45, ["1-2h"] = 90, [">2h"] = 150
        };

        double totalMinutes = 0;
        int totalSessions = 0;
        foreach (var (bucket, count) in data)
        {
            if (bucketMidpoints.TryGetValue(bucket, out double midpoint))
            {
                totalMinutes += midpoint * count;
                totalSessions += count;
            }
        }

        return totalSessions > 0 ? Math.Round(totalMinutes / totalSessions, 0) : 0;
    }
}
