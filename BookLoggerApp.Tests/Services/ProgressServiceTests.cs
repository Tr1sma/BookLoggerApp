using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ProgressServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BookRepository _bookRepository;
    private readonly MockProgressionService _progressionService;
    private readonly MockPlantService _plantService;
    private readonly MockBookService _bookService;
    private readonly ProgressService _service;

    public ProgressServiceTests()
    {
        _context = TestDbContext.Create();
        _bookRepository = new BookRepository(_context);
        var sessionRepository = new ReadingSessionRepository(_context);
        var goalRepository = new ReadingGoalRepository(_context);
        var plantRepository = new UserPlantRepository(_context);

        _unitOfWork = new UnitOfWork(_context, _bookRepository, sessionRepository, goalRepository, plantRepository);
        _progressionService = new MockProgressionService();
        _plantService = new MockPlantService();
        _bookService = new MockBookService();
        _service = new ProgressService(_unitOfWork, _progressionService, _plantService, _bookService);
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
        await _context.SaveChangesAsync();
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
        // Base: 30 minutes * 5 XP = 150, 20 pages * 20 XP = 400, Total = 550 (no bonuses for first session)
        result.XpEarned.Should().Be(550);
    }

    [Fact]
    public async Task AddSessionAsync_ShouldGiveBonusForLongSession()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var session = new ReadingSession
        {
            BookId = book.Id,
            Minutes = 60,
            PagesRead = 30
        };

        // Act
        var result = await _service.AddSessionAsync(session);

        // Assert
        // Base: 60 minutes * 5 XP = 300, 30 pages * 20 XP = 600, Bonus: 50 = 950
        result.XpEarned.Should().Be(950);
    }

    [Fact]
    public async Task GetTotalMinutesAsync_ShouldSumMinutesForBook()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
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
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        // Add sessions for today, yesterday, and day before yesterday
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today,
            Minutes = 30
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 30
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-2),
            Minutes = 30
        });
        await _unitOfWork.SaveChangesAsync();

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
        await _context.SaveChangesAsync();
        var threeDaysAgo = DateTime.UtcNow.AddDays(-3);

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = threeDaysAgo,
            Minutes = 30
        });
        await _unitOfWork.SaveChangesAsync();

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
        await _context.SaveChangesAsync();
        var session = await _service.StartSessionAsync(book.Id);

        // Simulate some time passing
        await Task.Delay(100);

        // Act
        var result = await _service.EndSessionAsync(session.Id, 10);

        // Assert
        result.Session.EndedAt.Should().NotBeNull();
        result.Session.Minutes.Should().BeGreaterThanOrEqualTo(0);
        result.Session.PagesRead.Should().Be(10);
        result.Session.XpEarned.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task EndSessionAsync_WithNegativePagesRead_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var book = await _bookService.AddAsync(new Book { Title = "Test", Author = "Author" });
        var session = await _service.StartSessionAsync(book.Id);

        // Act & Assert
        await FluentActions.Awaiting(() => _service.EndSessionAsync(session.Id, -10))
            .Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("pagesRead");
    }

    [Fact]
    public async Task EndSessionAsync_WithPagesExceedingBookPageCount_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var book = await _bookService.AddAsync(new Book
        {
            Title = "Test",
            Author = "Author",
            PageCount = 100
        });
        var session = await _service.StartSessionAsync(book.Id);

        // Act & Assert
        await FluentActions.Awaiting(() => _service.EndSessionAsync(session.Id, 150))
            .Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("pagesRead")
            .WithMessage("*exceeds book page count*");
    }

    [Fact]
    public async Task EndSessionAsync_WithPagesEqualToBookPageCount_ShouldSucceed()
    {
        // Arrange
        var book = await _bookService.AddAsync(new Book
        {
            Title = "Test",
            Author = "Author",
            PageCount = 100
        });
        var session = await _service.StartSessionAsync(book.Id);

        // Act
        var result = await _service.EndSessionAsync(session.Id, 100);

        // Assert
        result.Session.PagesRead.Should().Be(100);
    }

    [Fact]
    public async Task EndSessionAsync_WithBookWithoutPageCount_ShouldAllowAnyPositivePages()
    {
        // Arrange
        var book = await _bookService.AddAsync(new Book
        {
            Title = "Test",
            Author = "Author",
            PageCount = null
        });
        var session = await _service.StartSessionAsync(book.Id);

        // Act
        var result = await _service.EndSessionAsync(session.Id, 500);

        // Assert
        result.Session.PagesRead.Should().Be(500);
    }
}
