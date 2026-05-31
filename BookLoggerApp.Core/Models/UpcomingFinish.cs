namespace BookLoggerApp.Core.Models;

/// <summary>
/// A currently-reading book paired with its finish <see cref="ReadingForecast"/>,
/// for the Dashboard "Upcoming finishes" overview.
/// </summary>
public sealed record UpcomingFinish(
    Guid BookId,
    string Title,
    string Author,
    string? CoverImagePath,
    ReadingForecast Forecast);
