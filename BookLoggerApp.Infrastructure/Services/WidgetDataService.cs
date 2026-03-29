using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

public class WidgetDataService : IWidgetDataService
{
    private readonly IBookService _bookService;
    private readonly IProgressService _progressService;
    private readonly IGoalService _goalService;

    public WidgetDataService(
        IBookService bookService,
        IProgressService progressService,
        IGoalService goalService)
    {
        _bookService = bookService;
        _progressService = progressService;
        _goalService = goalService;
    }

    public async Task<WidgetData> GetWidgetDataAsync(CancellationToken ct = default)
    {
        var currentBook = (await _bookService.GetByStatusAsync(ReadingStatus.Reading, ct))
            .OrderByDescending(b => b.DateStarted ?? b.DateAdded)
            .FirstOrDefault();

        var streak = await _progressService.GetCurrentStreakAsync(ct);
        var dailyGoal = await _goalService.GetActiveDailyGoalAsync(ct);

        return new WidgetData
        {
            CurrentBookTitle = currentBook?.Title,
            CurrentBookProgressPercent = currentBook?.ProgressPercentage ?? 0,
            CurrentBookProgressText = currentBook == null
                ? "Kein aktives Buch"
                : currentBook.PageCount.HasValue
                    ? $"{currentBook.CurrentPage}/{currentBook.PageCount.Value} Seiten"
                    : $"{currentBook.CurrentPage} Seiten",
            StreakDays = streak,
            DailyGoalTitle = dailyGoal?.Title,
            DailyGoalProgressPercent = dailyGoal?.ProgressPercentage ?? 0,
            DailyGoalProgressText = dailyGoal == null
                ? "Kein Tagesziel"
                : $"{dailyGoal.Current}/{dailyGoal.Target} {GetGoalUnitLabel(dailyGoal.Type)}"
        };
    }

    private static string GetGoalUnitLabel(GoalType goalType)
    {
        return goalType switch
        {
            GoalType.Books => "Bücher",
            GoalType.Pages => "Seiten",
            GoalType.Minutes => "Minuten",
            _ => "Einheiten"
        };
    }
}
