namespace BookLoggerApp.Infrastructure.Services.Helpers;

/// <summary>
/// Helper class for calculating reading streaks from session dates.
/// </summary>
public static class StreakCalculator
{
    /// <summary>
    /// Calculates the current reading streak in days.
    /// </summary>
    /// <param name="dates">The list of dates when reading occurred.</param>
    /// <returns>The current streak count.</returns>
    public static int CalculateCurrentStreak(IEnumerable<DateTime> dates)
    {
        var sortedDates = dates
            .Select(d => d.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        if (!sortedDates.Any())
            return 0;

        var today = DateTime.UtcNow.Date;
        var mostRecentDate = sortedDates.First();

        // If the last read was more than 1 day ago (i.e., not today or yesterday), the streak is broken
        if ((today - mostRecentDate).Days > 1)
            return 0;

        int streak = 0;
        var currentDate = today;

        foreach (var date in sortedDates)
        {
            // Check if the date is consecutive (or same day for the start)
            if ((currentDate - date).Days <= 1)
            {
                streak++;
                currentDate = date;
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    /// <summary>
    /// Calculates the longest reading streak in days.
    /// </summary>
    /// <param name="dates">The list of dates when reading occurred.</param>
    /// <returns>The longest streak count.</returns>
    public static int CalculateLongestStreak(IEnumerable<DateTime> dates)
    {
        var sortedDates = dates
            .Select(d => d.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (!sortedDates.Any())
            return 0;

        int longestStreak = 1;
        int currentStreak = 1;

        for (int i = 1; i < sortedDates.Count; i++)
        {
            if ((sortedDates[i] - sortedDates[i - 1]).Days == 1)
            {
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else if ((sortedDates[i] - sortedDates[i - 1]).Days > 1)
            {
                currentStreak = 1;
            }
        }

        return longestStreak;
    }
}
