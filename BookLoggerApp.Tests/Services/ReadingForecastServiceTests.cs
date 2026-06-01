using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ReadingForecastServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly ReadingForecastService _service;

    public ReadingForecastServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new ReadingForecastService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<Book> SeedBookAsync(
        string title,
        ReadingStatus status,
        int? pageCount,
        int currentPage,
        DateTime? started,
        params (int daysAgo, int minutes, int? pages)[] sessions)
    {
        Book book = await _unitOfWork.Books.AddAsync(new Book
        {
            Title = title,
            Author = "Author",
            Status = status,
            PageCount = pageCount,
            CurrentPage = currentPage,
            DateStarted = started
        });
        await _context.SaveChangesAsync();

        foreach ((int daysAgo, int minutes, int? pages) in sessions)
        {
            await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
            {
                BookId = book.Id,
                StartedAt = DateTime.UtcNow.AddDays(-daysAgo),
                Minutes = minutes,
                PagesRead = pages
            });
        }
        await _context.SaveChangesAsync();
        return book;
    }

    [Fact]
    public async Task GetUpcomingFinishesAsync_WithReadingBooks_ReturnsForecastsOrderedBySoonest()
    {
        // A: almost done, brisk pace → finishes soon.
        Book soon = await SeedBookAsync("Soon", ReadingStatus.Reading, 120, 100, DateTime.UtcNow.AddDays(-6),
            (4, 30, 30), (2, 30, 30));
        // B: lots left, slow pace → finishes much later.
        Book later = await SeedBookAsync("Later", ReadingStatus.Reading, 600, 100, DateTime.UtcNow.AddDays(-12),
            (8, 30, 20), (4, 30, 20));

        IReadOnlyList<UpcomingFinish> result = await _service.GetUpcomingFinishesAsync();

        result.Should().HaveCount(2);
        result[0].BookId.Should().Be(soon.Id);
        result[1].BookId.Should().Be(later.Id);
        result[0].Forecast.ProjectedCompletionUtc.Should().BeOnOrBefore(result[1].Forecast.ProjectedCompletionUtc);
    }

    [Fact]
    public async Task GetUpcomingFinishesAsync_SkipsBookWithoutPageCount()
    {
        await SeedBookAsync("NoPages", ReadingStatus.Reading, null, 50, DateTime.UtcNow.AddDays(-5),
            (2, 30, 20));

        IReadOnlyList<UpcomingFinish> result = await _service.GetUpcomingFinishesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingFinishesAsync_SkipsBookWithoutUsableSessions()
    {
        await SeedBookAsync("NoSessions", ReadingStatus.Reading, 300, 0, DateTime.UtcNow.AddDays(-5));

        IReadOnlyList<UpcomingFinish> result = await _service.GetUpcomingFinishesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingFinishesAsync_NoReadingBooks_ReturnsEmpty()
    {
        await SeedBookAsync("Done", ReadingStatus.Completed, 200, 200, DateTime.UtcNow.AddDays(-20),
            (10, 30, 100));

        IReadOnlyList<UpcomingFinish> result = await _service.GetUpcomingFinishesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingFinishesAsync_IgnoresNonReadingBooks()
    {
        Book reading = await SeedBookAsync("Reading", ReadingStatus.Reading, 200, 50, DateTime.UtcNow.AddDays(-6),
            (4, 30, 20), (2, 30, 20));
        await SeedBookAsync("Completed", ReadingStatus.Completed, 200, 200, DateTime.UtcNow.AddDays(-20),
            (10, 30, 100));

        IReadOnlyList<UpcomingFinish> result = await _service.GetUpcomingFinishesAsync();

        result.Should().ContainSingle();
        result[0].BookId.Should().Be(reading.Id);
    }
}
