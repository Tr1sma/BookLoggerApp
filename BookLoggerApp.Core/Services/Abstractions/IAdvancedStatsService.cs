using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

public interface IAdvancedStatsService
{
    // Trends tab
    Task<Dictionary<DateTime, int>> GetReadingHeatmapAsync(int year, CancellationToken ct = default);
    Task<Dictionary<DayOfWeek, int>> GetWeekdayDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetTimeOfDayDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetSessionLengthDistributionAsync(CancellationToken ct = default);
    Task<Dictionary<int, int>> GetMonthlyVolumeAsync(int year, CancellationToken ct = default);
    Task<(double Current, double Previous)> GetReadingSpeedTrendAsync(CancellationToken ct = default);
    Task<(double CurrentAvg, double PreviousAvg)> GetAverageFinishTimeTrendAsync(CancellationToken ct = default);

    // Analysen tab
    Task<(YearStats Year1, YearStats Year2)> GetYearComparisonAsync(int year1, int year2, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetGenreRadarDataAsync(int maxGenres = 8, CancellationToken ct = default);
    Task<(int Completed, int Abandoned)> GetCompletionRateAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPageCountDistributionAsync(CancellationToken ct = default);
    Task<List<AuthorStats>> GetTopAuthorsAsync(int count = 5, CancellationToken ct = default);
}
