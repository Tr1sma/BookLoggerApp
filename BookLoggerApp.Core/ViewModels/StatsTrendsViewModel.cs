using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class StatsTrendsViewModel : ViewModelBase
{
    private readonly IAdvancedStatsService _advancedStatsService;

    public StatsTrendsViewModel(IAdvancedStatsService advancedStatsService)
    {
        _advancedStatsService = advancedStatsService;
        _heatmapYear = DateTime.UtcNow.Year;
    }

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
        await ExecuteSafelyWithDbAsync(async () =>
        {
            var heatmapTask = _advancedStatsService.GetReadingHeatmapAsync(HeatmapYear);
            var weekdayTask = _advancedStatsService.GetWeekdayDistributionAsync();
            var timeOfDayTask = _advancedStatsService.GetTimeOfDayDistributionAsync();
            var sessionLengthTask = _advancedStatsService.GetSessionLengthDistributionAsync();
            var monthlyTask = _advancedStatsService.GetMonthlyVolumeAsync(DateTime.UtcNow.Year);
            var speedTask = _advancedStatsService.GetReadingSpeedTrendAsync();
            var finishTask = _advancedStatsService.GetAverageFinishTimeTrendAsync();

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
        }, "Fehler beim Laden der Trend-Statistiken");
    }

    [RelayCommand]
    public async Task ChangeHeatmapYearAsync(int year)
    {
        HeatmapYear = year;
        HeatmapData = await _advancedStatsService.GetReadingHeatmapAsync(year);
    }

    private static string DetermineTimeOfDayLabel(Dictionary<string, int> data)
    {
        if (data.Count == 0 || data.Values.All(v => v == 0))
            return "";

        string dominant = data.MaxBy(kv => kv.Value).Key;
        return dominant switch
        {
            "Morning" => "Frühleser 🌅",
            "Afternoon" => "Tagträumer ☀️",
            "Evening" => "Abendleser 🌙",
            "Night" => "Nachteule 🦉",
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
