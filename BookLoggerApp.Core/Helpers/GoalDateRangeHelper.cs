using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Single source of truth for translating a goal's user-picked date range into a UTC
/// range for comparison against UTC Book/ReadingSession timestamps.
/// </summary>
/// <remarks>
/// Goal dates arrive as Kind=Unspecified local calendar values; Book.DateCompleted and
/// ReadingSession.StartedAt are UTC. Without ToUniversalTime(), a book finished just after
/// local midnight could count in the wrong day/month/year in non-UTC zones, since DateTime
/// comparison uses raw ticks and ignores Kind (CODE_REVIEW INK-01).
/// </remarks>
public static class GoalDateRangeHelper
{
    /// <summary>
    /// Returns the UTC range for the goal's calendar window (inclusive of the entire end day).
    /// ToUniversalTime() is a no-op for Kind=Utc dates (keeping legacy tests unaffected)
    /// and treats Kind=Unspecified/Local as local time.
    /// </summary>
    public static (DateTime UtcStart, DateTime UtcEnd) GetGoalRangeUtc(ReadingGoal goal)
    {
        var utcStart = goal.StartDate.Date.ToUniversalTime();
        var utcEnd = goal.EndDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        return (utcStart, utcEnd);
    }
}
