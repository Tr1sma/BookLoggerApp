using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Produces finish forecasts across all currently-reading books for overview surfaces
/// (e.g. the Dashboard "Upcoming finishes" widget).
/// </summary>
public interface IReadingForecastService
{
    /// <summary>
    /// Builds a forecast for every book in <see cref="ReadingStatus.Reading"/> that has
    /// enough data, ordered by soonest projected completion. Books that can't be forecast
    /// (no page count, no usable sessions) are omitted.
    /// </summary>
    Task<IReadOnlyList<UpcomingFinish>> GetUpcomingFinishesAsync(CancellationToken ct = default);
}
