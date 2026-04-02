namespace BookLoggerApp.Platforms.Android.Widgets.Models;

/// <summary>
/// Lightweight DTOs for widget display — avoids passing EF entities to RemoteViews code.
/// </summary>
public record CurrentBookWidgetData(
    Guid BookId,
    string Title,
    string Author,
    int CurrentPage,
    int TotalPages,
    int ProgressPercentage,
    string? CoverImagePath);

public record StreakWidgetData(
    int CurrentStreak,
    bool ReadToday);

public record GoalWidgetData(
    Guid GoalId,
    string Title,
    string GoalType,
    int Current,
    int Target,
    int ProgressPercentage,
    DateTime EndDate);
