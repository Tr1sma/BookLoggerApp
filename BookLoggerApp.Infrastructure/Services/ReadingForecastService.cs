using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Builds finish forecasts for currently-reading books; prediction math lives in
/// <see cref="ReadingForecastCalculator"/>. Sessions fetched in one ranged query to avoid N+1.
/// </summary>
public class ReadingForecastService : IReadingForecastService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReadingForecastService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<UpcomingFinish>> GetUpcomingFinishesAsync(CancellationToken ct = default)
    {
        List<Book> readingBooks = (await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Reading, ct)).ToList();
        if (readingBooks.Count == 0)
        {
            return Array.Empty<UpcomingFinish>();
        }

        DateTime now = DateTime.UtcNow;

        // Generous default window covers books without an explicit DateStarted.
        DateTime earliest = now.AddDays(-365);
        foreach (Book book in readingBooks)
        {
            if (book.DateStarted is { } started && started < earliest)
            {
                earliest = started;
            }
        }

        var sessionsByBook = (await _unitOfWork.ReadingSessions.GetSessionsInRangeAsync(earliest, now, ct))
            .GroupBy(s => s.BookId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ReadingSession>)g.ToList());

        var results = new List<UpcomingFinish>();
        foreach (Book book in readingBooks)
        {
            IReadOnlyList<ReadingSession> sessions = sessionsByBook.TryGetValue(book.Id, out IReadOnlyList<ReadingSession>? s)
                ? s
                : Array.Empty<ReadingSession>();

            ReadingForecast? forecast = ReadingForecastCalculator.TryBuildForecast(book, sessions, now);
            if (forecast is not null)
            {
                results.Add(new UpcomingFinish(book.Id, book.Title, book.Author, book.CoverImagePath, forecast));
            }
        }

        return results
            .OrderBy(r => r.Forecast.ProjectedCompletionUtc)
            .ToList();
    }
}
