namespace BookLoggerApp.Core.Models;

/// <summary>
/// A data-driven prediction of when a book will be finished, derived from the
/// user's historical reading sessions. Produced by
/// <see cref="Helpers.ReadingForecastCalculator"/>.
/// </summary>
public sealed record ReadingForecast
{
    /// <summary>Pages left to read, anchored to <see cref="Book.CurrentPage"/>.</summary>
    public int PagesRemaining { get; init; }

    public int PageCount { get; init; }

    public int CurrentPage { get; init; }

    /// <summary>Calendar-based reading rate (rest days dilute it).</summary>
    public double AveragePagesPerDay { get; init; }

    /// <summary>Mean per-session reading speed in pages per hour.</summary>
    public double AveragePagesPerHour { get; init; }

    public double AverageSessionMinutes { get; init; }

    /// <summary>Population standard deviation of per-session pages/hour (0 with &lt;2 sessions).</summary>
    public double PagesPerHourStdDev { get; init; }

    /// <summary>Point-estimate completion date (UTC).</summary>
    public DateTime ProjectedCompletionUtc { get; init; }

    /// <summary>Earlier bound — faster pace (UTC).</summary>
    public DateTime OptimisticCompletionUtc { get; init; }

    /// <summary>Later bound — slower pace (UTC).</summary>
    public DateTime PessimisticCompletionUtc { get; init; }

    public int ProjectedDaysRemaining { get; init; }

    public ForecastConfidence Confidence { get; init; }

    /// <summary>Number of sessions that contributed page data to the forecast.</summary>
    public int SessionsUsed { get; init; }

    /// <summary>Historical pages-remaining over time (ascending by date).</summary>
    public IReadOnlyList<BurnDownPoint> ActualSeries { get; init; } = [];

    /// <summary>Projected pages-remaining: (now, remaining) → (projected, 0).</summary>
    public IReadOnlyList<BurnDownPoint> ProjectedSeries { get; init; } = [];

    /// <summary>True when the optimistic/pessimistic band is non-degenerate.</summary>
    public bool HasRange => OptimisticCompletionUtc != PessimisticCompletionUtc;
}

/// <summary>A single point on a burn-down chart: pages remaining at a date.</summary>
public sealed record BurnDownPoint(DateTime Date, int PagesRemaining);

/// <summary>How trustworthy a <see cref="ReadingForecast"/> is, based on session count and speed variance.</summary>
public enum ForecastConfidence
{
    Low = 1,
    Medium = 2,
    High = 3
}

/// <summary>Tunable knobs for <see cref="Helpers.ReadingForecastCalculator.TryBuildForecast"/>.</summary>
public sealed record ReadingForecastOptions(double ConfidenceK = 1.0, int RecentWindowDays = 30);
