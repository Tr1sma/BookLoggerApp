using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class StatsAnalysesViewModel : ViewModelBase
{
    private readonly IAdvancedStatsService _advancedStatsService;
    private readonly IStatsService _statsService;

    public StatsAnalysesViewModel(IAdvancedStatsService advancedStatsService, IStatsService statsService)
    {
        _advancedStatsService = advancedStatsService;
        _statsService = statsService;
    }

    // Year comparison
    [ObservableProperty]
    private YearStats _year1Stats = new(0, 0, 0, 0, 0);

    [ObservableProperty]
    private YearStats _year2Stats = new(0, 0, 0, 0, 0);

    [ObservableProperty]
    private List<int> _availableYears = new();

    [ObservableProperty]
    private int _selectedYear1;

    [ObservableProperty]
    private int _selectedYear2;

    // Genre radar
    [ObservableProperty]
    private Dictionary<string, int> _genreRadarData = new();

    // Completion rate
    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _abandonedCount;

    [ObservableProperty]
    private double _completionPercentage;

    // Page count distribution
    [ObservableProperty]
    private Dictionary<string, int> _pageCountData = new();

    // Top authors
    [ObservableProperty]
    private List<AuthorStats> _topAuthors = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            // Load available years first
            var periods = await _statsService.GetActiveReadingPeriodsAsync();
            AvailableYears = periods.Select(p => p.Year).Distinct().OrderByDescending(y => y).ToList();

            if (AvailableYears.Count >= 2)
            {
                SelectedYear1 = AvailableYears[1];
                SelectedYear2 = AvailableYears[0];
            }
            else if (AvailableYears.Count == 1)
            {
                SelectedYear1 = AvailableYears[0] - 1;
                SelectedYear2 = AvailableYears[0];
            }
            else
            {
                SelectedYear1 = DateTime.UtcNow.Year - 1;
                SelectedYear2 = DateTime.UtcNow.Year;
            }

            var yearTask = _advancedStatsService.GetYearComparisonAsync(SelectedYear1, SelectedYear2);
            var genreTask = _advancedStatsService.GetGenreRadarDataAsync();
            var completionTask = _advancedStatsService.GetCompletionRateAsync();
            var pageCountTask = _advancedStatsService.GetPageCountDistributionAsync();
            var authorsTask = _advancedStatsService.GetTopAuthorsAsync(5);

            await Task.WhenAll(yearTask, genreTask, completionTask, pageCountTask, authorsTask);

            var (y1, y2) = yearTask.Result;
            Year1Stats = y1;
            Year2Stats = y2;

            GenreRadarData = genreTask.Result;

            var (completed, abandoned) = completionTask.Result;
            CompletedCount = completed;
            AbandonedCount = abandoned;
            int total = completed + abandoned;
            CompletionPercentage = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0;

            PageCountData = pageCountTask.Result;
            TopAuthors = authorsTask.Result;
        }, "Fehler beim Laden der Analyse-Statistiken");
    }

    [RelayCommand]
    public async Task ChangeComparisonYearsAsync((int year1, int year2) years)
    {
        SelectedYear1 = years.year1;
        SelectedYear2 = years.year2;
        var (y1, y2) = await _advancedStatsService.GetYearComparisonAsync(years.year1, years.year2);
        Year1Stats = y1;
        Year2Stats = y2;
    }
}
