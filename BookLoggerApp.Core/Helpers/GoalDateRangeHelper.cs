using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Single source of truth for translating a goal's user-picked date range into a UTC
/// range suitable for comparing against UTC timestamps on Books/ReadingSessions.
///
/// Goal dates arrive from the UI's &lt;input type="date"&gt; binding as Kind=Unspecified
/// and represent the user's local calendar; Book.DateCompleted and ReadingSession.EndedAt
/// are written as DateTime.UtcNow. Without the ToUniversalTime() conversion, a book
/// finished just after local midnight could count in the wrong day/month/year for
/// users in non-UTC timezones, since DateTime comparison uses raw ticks and ignores Kind.
/// </summary>
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
