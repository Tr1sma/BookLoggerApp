using System;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Converts canonically-UTC reading timestamps into the user's local calendar for day,
/// weekday and time-of-day bucketing, keeping streaks/stats/goals on one calendar-day
/// convention (CODE_REVIEW LOG-02/LOG-04/LOG-06/LOG-08, INK-06).
/// </summary>
/// <remarks>
/// Time zone is caller-supplied (production passes <see cref="TimeZoneInfo.Local"/>) so the
/// logic stays deterministically testable on a CI machine in any zone.
/// </remarks>
public static class LocalTimeHelper
{
    /// <summary>
    /// Treats <paramref name="utcTimestamp"/> as UTC (regardless of its <see cref="DateTimeKind"/>,
    /// since SQLite reads values back as <see cref="DateTimeKind.Unspecified"/>) and converts it to
    /// <paramref name="timeZone"/>.
    /// </summary>
    public static DateTime ToLocal(DateTime utcTimestamp, TimeZoneInfo timeZone)
    {
        if (timeZone is null)
        {
            throw new ArgumentNullException(nameof(timeZone));
        }

        DateTime asUtc = DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, timeZone);
    }

    /// <summary>
    /// The local calendar date (midnight) on which a UTC timestamp falls in
    /// <paramref name="timeZone"/>.
    /// </summary>
    public static DateTime LocalDate(DateTime utcTimestamp, TimeZoneInfo timeZone)
    {
        return ToLocal(utcTimestamp, timeZone).Date;
    }
}
