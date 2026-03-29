namespace BookLoggerApp.Core.Models;

public class WidgetData
{
    public string? CurrentBookTitle { get; set; }
    public int CurrentBookProgressPercent { get; set; }
    public string CurrentBookProgressText { get; set; } = string.Empty;
    public int StreakDays { get; set; }
    public string? DailyGoalTitle { get; set; }
    public int DailyGoalProgressPercent { get; set; }
    public string DailyGoalProgressText { get; set; } = string.Empty;
}
