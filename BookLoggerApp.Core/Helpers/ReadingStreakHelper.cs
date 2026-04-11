using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

public static class ReadingStreakHelper
{
    public static bool CountsTowardStreak(ReadingSession session)
    {
        return session.Minutes > 0
            || (session.PagesRead ?? 0) > 0;
    }

    public static int CalculateCurrentStreak(IEnumerable<ReadingSession> sessions, DateTime today)
    {
        return CalculateCurrentStreak(GetQualifyingDates(sessions), today);
    }

    public static int CalculateCurrentStreak(IEnumerable<DateTime> sessionDates, DateTime today)
    {
        var dates = sessionDates
            .Select(date => date.Date)
            .ToHashSet();

        if (dates.Count == 0)
        {
            return 0;
        }

        var normalizedToday = today.Date;
        if (dates.Contains(normalizedToday))
        {
            return CountConsecutiveDays(dates, normalizedToday);
        }

        var yesterday = normalizedToday.AddDays(-1);
        if (dates.Contains(yesterday))
        {
            return CountConsecutiveDays(dates, yesterday);
        }

        return 0;
    }

    public static int CalculateInclusiveStreak(IEnumerable<ReadingSession> sessions, DateTime anchorDate)
    {
        return CalculateInclusiveStreak(GetQualifyingDates(sessions), anchorDate);
    }

    public static int CalculateInclusiveStreak(IEnumerable<DateTime> sessionDates, DateTime anchorDate)
    {
        var dates = sessionDates
            .Select(date => date.Date)
            .ToHashSet();

        dates.Add(anchorDate.Date);

        return CountConsecutiveDays(dates, anchorDate.Date);
    }

    public static int CalculateLongestStreak(IEnumerable<ReadingSession> sessions)
    {
        return CalculateLongestStreak(GetQualifyingDates(sessions));
    }

    public static int CalculateLongestStreak(IEnumerable<DateTime> sessionDates)
    {
        var dates = sessionDates
            .Select(date => date.Date)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        if (dates.Count == 0)
        {
            return 0;
        }

        int longestStreak = 1;
        int currentStreak = 1;

        for (int i = 1; i < dates.Count; i++)
        {
            if ((dates[i] - dates[i - 1]).Days == 1)
            {
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else if ((dates[i] - dates[i - 1]).Days > 1)
            {
                currentStreak = 1;
            }
        }

        return longestStreak;
    }

    private static IEnumerable<DateTime> GetQualifyingDates(IEnumerable<ReadingSession> sessions)
    {
        return sessions
            .Where(CountsTowardStreak)
            .Select(session => session.StartedAt.Date)
            .Distinct();
    }

    private static int CountConsecutiveDays(HashSet<DateTime> sessionDates, DateTime startDate)
    {
        int streak = 0;
        var currentDate = startDate.Date;

        while (sessionDates.Contains(currentDate))
        {
            streak++;
            currentDate = currentDate.AddDays(-1);
        }

        return streak;
    }
}
