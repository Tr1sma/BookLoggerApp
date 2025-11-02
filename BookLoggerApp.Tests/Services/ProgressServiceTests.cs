using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ProgressServiceTests : IDisposable
{
    private readonly Infrastructure.Data.AppDbContext _context;
    private readonly ReadingSessionRepository _sessionRepository;
    private readonly BookRepository _bookRepository;
    private readonly ProgressService _service;

    public ProgressServiceTests()
    {
        _context = TestDbContext.Create();
        _sessionRepository = new ReadingSessionRepository(_context);
        _bookRepository = new BookRepository(_context);
        _service = new ProgressService(_sessionRepository);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task AddSessionAsync_ShouldCalculateXp()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test", Author = "Author" });
        var session = new ReadingSession
        {
            BookId = book.Id,
            Minutes = 30,
            PagesRead = 20
        };

        // Act
        var result = await _service.AddSessionAsync(session);

        // Assert
        result.XpEarned.Should().BeGreaterThan(0);
        // Base: 30 minutes * 1 XP = 30, 20 pages * 2 XP = 40, Total = 70 (no bonuses for first session)
        result.XpEarned.Should().Be(70);
    }

    [Fact]
    public async Task AddSessionAsync_ShouldGiveBonusForLongSession()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test", Author = "Author" });
        var session = new ReadingSession
        {
            BookId = book.Id,
            Minutes = 60,
            PagesRead = 30
        };

        // Act
        var result = await _service.AddSessionAsync(session);

        // Assert
        // Base: 60 minutes * 1 XP = 60, 30 pages * 2 XP = 60, Bonus: 50 = 170
        result.XpEarned.Should().Be(170);
    }

    [Fact]
    public async Task GetTotalMinutesAsync_ShouldSumMinutesForBook()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _service.AddSessionAsync(new ReadingSession { BookId = book.Id, Minutes = 30 });
        await _service.AddSessionAsync(new ReadingSession { BookId = book.Id, Minutes = 45 });
        await _service.AddSessionAsync(new ReadingSession { BookId = book.Id, Minutes = 15 });

        // Act
        var total = await _service.GetTotalMinutesAsync(book.Id);

        // Assert
        total.Should().Be(90);
    }

    [Fact]
    public async Task GetCurrentStreakAsync_ShouldCalculateStreak()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test", Author = "Author" });
        var today = DateTime.UtcNow.Date;

        // Add sessions for today, yesterday, and day before yesterday
        await _sessionRepository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today,
            Minutes = 30
        });
        await _sessionRepository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 30
        });
        await _sessionRepository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-2),
            Minutes = 30
        });

        // Act
        var streak = await _service.GetCurrentStreakAsync();

        // Assert
        streak.Should().Be(3);
    }

    [Fact]
    public async Task GetCurrentStreakAsync_ShouldReturnZeroIfNoRecentSession()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test", Author = "Author" });
        var threeDaysAgo = DateTime.UtcNow.AddDays(-3);

        await _sessionRepository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = threeDaysAgo,
            Minutes = 30
        });

        // Act
        var streak = await _service.GetCurrentStreakAsync();

        // Assert
        streak.Should().Be(0); // Streak broken
    }

    [Fact]
    public async Task EndSessionAsync_ShouldCalculateDurationAndXp()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test", Author = "Author" });
        var session = await _service.StartSessionAsync(book.Id);

        // Simulate some time passing
        await Task.Delay(100);

        // Act
        var ended = await _service.EndSessionAsync(session.Id, 10);

        // Assert
        ended.EndedAt.Should().NotBeNull();
        ended.Minutes.Should().BeGreaterThanOrEqualTo(0);
        ended.PagesRead.Should().Be(10);
        ended.XpEarned.Should().BeGreaterThan(0);
    }
}
