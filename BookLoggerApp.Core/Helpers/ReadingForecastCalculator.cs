using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Pure, stateless calculator that turns a book's reading history into a
/// <see cref="ReadingForecast"/> (predicted finish date + confidence band + burn-down series).
/// <para>
/// All time is injected via <paramref name="nowUtc"/> so the calculation is deterministic
/// and unit-testable — never call <see cref="DateTime.UtcNow"/> inside.
/// </para>
/// </summary>
public static class ReadingForecastCalculator
{
    /// <summary>Maximum projected horizon, guards against absurd dates / int overflow on tiny rates.</summary>
    private const int MaxProjectedDays = 3650; // 10 years

    /// <summary>Sessions shorter than this are ignored — too short to reflect a real reading pace.</summary>
    private const int MinimumSessionMinutes = 5;

    /// <summary>
    /// Builds a forecast for <paramref name="book"/> from <paramref name="sessions"/>, or returns
    /// <c>null</c> when there is not enough data to make a meaningful prediction (caller hides the UI).
    /// </summary>
    public static ReadingForecast? TryBuildForecast(
        Book book,
        IReadOnlyList<ReadingSession> sessions,
        DateTime nowUtc,
        ReadingForecastOptions? options = null)
    {
        ReadingForecastOptions opts = options ?? new ReadingForecastOptions();

        // 1. Need a known total and pages still to read.
        if (book.PageCount is not > 0)
        {
            return null;
        }

        int pageCount = book.PageCount.Value;
        int pagesRemaining = Math.Clamp(pageCount - book.CurrentPage, 0, pageCount);
        if (pagesRemaining <= 0)
        {
            return null;
        }

        // Sessions arrive newest-first from the repository — normalise to ascending.
        List<ReadingSession> ordered = sessions.OrderBy(s => s.StartedAt).ToList();
        List<ReadingSession> counted = ordered
            .Where(s => s.PagesRead is > 0 && s.Minutes >= MinimumSessionMinutes)
            .ToList();

        DateTime firstSessionDate = ordered.Count > 0 ? ordered[0].StartedAt : nowUtc;
        DateTime activeStart = book.DateStarted ?? firstSessionDate;

        double pagesPerDay;
        double meanPph = 0;
        double sigma = 0;
        double avgSessionMinutes = 0;

        if (counted.Count > 0)
        {
            // 2. Calendar-based pages/day over a recent rolling window (rest days dilute the rate).
            DateTime recentWindowStart = nowUtc.AddDays(-opts.RecentWindowDays);
            DateTime windowStart = activeStart > recentWindowStart ? activeStart : recentWindowStart;

            int windowPages = counted.Where(s => s.StartedAt >= windowStart).Sum(s => s.PagesRead!.Value);
            double windowDays = Math.Max(1, Math.Ceiling((nowUtc - windowStart).TotalDays));
            pagesPerDay = windowPages / windowDays;

            if (windowPages <= 0)
            {
                // Recent window empty — fall back to the whole active window.
                int allPages = counted.Sum(s => s.PagesRead!.Value);
                double allDays = Math.Max(1, Math.Ceiling((nowUtc - activeStart).TotalDays));
                pagesPerDay = allPages / allDays;
            }

            // 3. Per-session speed (pages/hour) → mean + dispersion for the confidence band.
            List<double> speeds = counted.Select(s => s.PagesRead!.Value / (s.Minutes / 60.0)).ToList();
            meanPph = speeds.Average();
            sigma = PopulationStdDev(speeds, meanPph);
            avgSessionMinutes = counted.Average(s => s.Minutes);
        }
        else
        {
            // Fallback: time-only sessions (no page data). Derive a coarse rate from the
            // current-page progression since the book was started. No variance → collapsed band.
            if (book.DateStarted is null || book.CurrentPage <= 0)
            {
                return null;
            }

            double elapsedDays = Math.Max(1, Math.Ceiling((nowUtc - book.DateStarted.Value).TotalDays));
            pagesPerDay = book.CurrentPage / elapsedDays;
        }

        if (pagesPerDay <= 0)
        {
            return null;
        }

        // 4. Confidence band from the relative dispersion (coefficient of variation) of speed.
        double cv = meanPph > 0 ? Math.Clamp(sigma / meanPph, 0, 0.75) : 0;
        double k = opts.ConfidenceK;
        double fastRate = Math.Min(pagesPerDay * (1 + (k * cv)), pagesPerDay * 4.0);
        double slowRate = Math.Max(pagesPerDay * (1 - (k * cv)), pagesPerDay * 0.25);

        // 5. Days → dates (clamped so a tiny rate can't overflow DateTime).
        int projectedDays = DaysFor(pagesRemaining, pagesPerDay);
        int optimisticDays = DaysFor(pagesRemaining, fastRate);   // faster → fewer days → earlier
        int pessimisticDays = DaysFor(pagesRemaining, slowRate);  // slower → more days → later

        DateTime projected = nowUtc.AddDays(projectedDays);
        DateTime optimistic = nowUtc.AddDays(optimisticDays);
        DateTime pessimistic = nowUtc.AddDays(pessimisticDays);

        // 6. Series.
        IReadOnlyList<BurnDownPoint> actualSeries = BuildActualSeries(book, counted, pageCount, pagesRemaining, activeStart, nowUtc);
        IReadOnlyList<BurnDownPoint> projectedSeries = new List<BurnDownPoint>
        {
            new(nowUtc, pagesRemaining),
            new(projected, 0)
        };

        // 7. Confidence level.
        ForecastConfidence confidence = ClassifyConfidence(counted.Count, cv);

        return new ReadingForecast
        {
            PagesRemaining = pagesRemaining,
            PageCount = pageCount,
            CurrentPage = book.CurrentPage,
            AveragePagesPerDay = pagesPerDay,
            AveragePagesPerHour = meanPph,
            AverageSessionMinutes = avgSessionMinutes,
            PagesPerHourStdDev = sigma,
            ProjectedCompletionUtc = projected,
            OptimisticCompletionUtc = optimistic,
            PessimisticCompletionUtc = pessimistic,
            ProjectedDaysRemaining = projectedDays,
            Confidence = confidence,
            SessionsUsed = counted.Count,
            ActualSeries = actualSeries,
            ProjectedSeries = projectedSeries
        };
    }

    private static List<BurnDownPoint> BuildActualSeries(
        Book book,
        List<ReadingSession> counted,
        int pageCount,
        int pagesRemaining,
        DateTime activeStart,
        DateTime nowUtc)
    {
        var series = new List<BurnDownPoint>();

        DateTime startDate = counted.Count > 0
            ? Min(activeStart, counted[0].StartedAt)
            : activeStart;

        // Start anchor: book not yet read, all pages remaining.
        series.Add(new BurnDownPoint(startDate.Date, pageCount));

        // Rebase so the latest reconstructed point lands exactly on the authoritative
        // pages-remaining (session sums can diverge from Book.CurrentPage).
        int sessionPagesTotal = counted.Sum(s => s.PagesRead!.Value);
        int delta = book.CurrentPage - sessionPagesTotal;

        int cumulative = 0;
        foreach (IGrouping<DateTime, ReadingSession> group in counted
            .GroupBy(s => s.StartedAt.Date)
            .OrderBy(g => g.Key))
        {
            cumulative += group.Sum(s => s.PagesRead!.Value);
            int remaining = Math.Clamp(pageCount - (cumulative + delta), 0, pageCount);
            if (group.Key > startDate.Date)
            {
                series.Add(new BurnDownPoint(group.Key, remaining));
            }
        }

        // Final actual point at "now" — the anchor the projection grows from.
        series.Add(new BurnDownPoint(nowUtc, pagesRemaining));
        return series;
    }

    private static ForecastConfidence ClassifyConfidence(int countedSessions, double cv)
    {
        if (countedSessions <= 3 || cv > 0.6)
        {
            return ForecastConfidence.Low;
        }

        if (countedSessions >= 8 && cv <= 0.35)
        {
            return ForecastConfidence.High;
        }

        return ForecastConfidence.Medium;
    }

    private static int DaysFor(int pagesRemaining, double rate)
    {
        if (rate <= 0)
        {
            return MaxProjectedDays;
        }

        double days = Math.Ceiling(pagesRemaining / rate);
        if (double.IsNaN(days) || days < 0)
        {
            days = 0;
        }

        return (int)Math.Min(days, MaxProjectedDays);
    }

    private static double PopulationStdDev(IReadOnlyList<double> values, double mean)
    {
        if (values.Count < 2)
        {
            return 0;
        }

        double sumSquares = 0;
        foreach (double v in values)
        {
            double d = v - mean;
            sumSquares += d * d;
        }

        return Math.Sqrt(sumSquares / values.Count);
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
