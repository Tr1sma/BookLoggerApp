using FluentAssertions;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ProgressServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MockProgressionService _progressionService;
    private readonly MockPlantService _plantService;
    private readonly MockBookService _bookService;
    private readonly MockGoalService _goalService;
    private readonly IDecorationService _decorationService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly ProgressService _service;

    public ProgressServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _progressionService = new MockProgressionService();
        _plantService = new MockPlantService();
        _bookService = new MockBookService();
        _goalService = new MockGoalService();
        _decorationService = Substitute.For<IDecorationService>();
        _decorationService.UserOwnsAbilityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        _settingsProvider = Substitute.For<IAppSettingsProvider>();
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AppSettings { UserLevel = 1, TotalXp = 0, Coins = 0 }));
        _service = new ProgressService(
            _unitOfWork, _progressionService, _plantService, _bookService,
            _goalService, _decorationService, _settingsProvider);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task AddSessionAsync_ShouldCalculateXp()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
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
        result.Session.XpEarned.Should().BeGreaterThan(0);
        // Base: 30 minutes * 5 XP = 150, 20 pages * 20 XP = 400, Total = 550 (no bonuses for first session)
        result.Session.XpEarned.Should().Be(550);
    }

    [Fact]
    public async Task AddSessionAsync_ShouldGiveBonusForLongSession()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
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
        result.Session.XpEarned.Should().Be(950);
    }

    [Fact]
    public async Task AddSessionAsync_ShouldReturnGoalCompletedFlagFromGoalService()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        _goalService.NextRecalculateGoalProgressResult = true;

        // Act
        var result = await _service.AddSessionAsync(new ReadingSession
        {
            BookId = book.Id,
            Minutes = 15
        });

        // Assert
        result.GoalCompleted.Should().BeTrue();
        _goalService.RecalculateGoalProgressCallCount.Should().Be(1);
    }

    [Fact]
    public async Task AddSessionAsync_ShouldApplyScaledStreakBonus_ForSessionDate()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 15
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-2),
            Minutes = 15
        });
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _service.AddSessionAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today,
            Minutes = 10,
            PagesRead = 1
        });

        // Assert
        result.ProgressionResult.StreakDays.Should().Be(3);
        result.ProgressionResult.StreakBonusXp.Should().Be(400);
        result.Session.XpEarned.Should().Be(470);
    }

    [Fact]
    public async Task AddSessionAsync_ShouldAwardStreakBonus_OnlyForFirstQualifyingSessionOfDay()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 15
        });
        await _unitOfWork.SaveChangesAsync();

        await _service.AddSessionAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today,
            Minutes = 10
        });

        // Act
        var result = await _service.AddSessionAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddHours(2),
            Minutes = 12,
            PagesRead = 3
        });

        // Assert
        result.ProgressionResult.StreakDays.Should().Be(0);
        result.ProgressionResult.StreakBonusXp.Should().Be(0);
        result.Session.XpEarned.Should().Be(120);
    }

    [Fact]
    public async Task AddSessionAsync_ShouldIgnoreOpenPlaceholderSessions_WhenCalculatingStreak()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 0,
            PagesRead = 0,
            EndedAt = null
        });
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _service.AddSessionAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today,
            Minutes = 10
        });

        // Assert
        result.ProgressionResult.StreakDays.Should().Be(1);
        result.ProgressionResult.StreakBonusXp.Should().Be(0);
        result.Session.XpEarned.Should().Be(50);
    }

    [Fact]
    public async Task GetTotalMinutesAsync_ShouldSumMinutesForBook()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
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
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
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
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
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
    public async Task GetCurrentStreakAsync_ShouldIgnoreOpenPlaceholderSessions()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today,
            Minutes = 15
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 0,
            PagesRead = 0,
            EndedAt = null
        });
        await _unitOfWork.SaveChangesAsync();

        // Act
        var streak = await _service.GetCurrentStreakAsync();

        // Assert
        streak.Should().Be(1);
    }

    [Fact]
    public async Task EndSessionAsync_ShouldCalculateDurationAndXp()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
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
    public async Task EndSessionAsync_ShouldReturnGoalCompletedFlagFromGoalService()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var session = await _service.StartSessionAsync(book.Id);
        _goalService.NextRecalculateGoalProgressResult = true;

        // Act
        var result = await _service.EndSessionAsync(session.Id, 10);

        // Assert
        result.GoalCompleted.Should().BeTrue();
        _goalService.RecalculateGoalProgressCallCount.Should().Be(1);
    }

    [Fact]
    public async Task EndSessionAsync_ShouldApplyScaledStreakBonus_ForActiveSessionDate()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 15
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-2),
            Minutes = 15
        });

        var session = await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            Minutes = 0
        });
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _service.EndSessionAsync(session.Id, 1);

        // Assert
        result.ProgressionResult.StreakDays.Should().Be(3);
        result.ProgressionResult.StreakBonusXp.Should().Be(400);
        result.Session.XpEarned.Should().Be(470);
    }

    [Fact]
    public async Task EndSessionAsync_ShouldAwardStreakBonus_OnlyForFirstQualifyingSessionOfDay()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 15
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today,
            Minutes = 20,
            PagesRead = 5,
            EndedAt = today.AddMinutes(20)
        });

        var session = await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            Minutes = 0
        });
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _service.EndSessionAsync(session.Id, 1);

        // Assert
        result.ProgressionResult.StreakDays.Should().Be(0);
        result.ProgressionResult.StreakBonusXp.Should().Be(0);
        result.Session.XpEarned.Should().Be(70);
    }

    [Fact]
    public async Task EndSessionAsync_ShouldNotAwardStreakBonus_ForZeroProgressSession()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-1),
            Minutes = 15
        });

        var session = await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow,
            Minutes = 0
        });
        await _unitOfWork.SaveChangesAsync();

        // Act
        var result = await _service.EndSessionAsync(session.Id, 0);

        // Assert
        result.ProgressionResult.StreakDays.Should().Be(0);
        result.ProgressionResult.StreakBonusXp.Should().Be(0);
        result.Session.XpEarned.Should().Be(0);
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
    public async Task EndSessionAsync_WithHighStartPageAndTooLargeDelta_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var book = await _bookService.AddAsync(new Book
        {
            Title = "Test",
            Author = "Author",
            PageCount = 100
        });
        var session = await _service.StartSessionAsync(book.Id);
        session.StartPage = 95;
        await _service.UpdateSessionAsync(session);

        // Act & Assert
        await FluentActions.Awaiting(() => _service.EndSessionAsync(session.Id, 10))
            .Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("pagesRead")
            .WithMessage("*Session end page*exceeds book page count*");
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

    // ─────────────────────────────────────────────────────────────────────────
    // Coverage-ergänzende Tests (DeleteSessionAsync, Getter-Methoden,
    // Update, GetMinutesByDateAsync, GetTotalMinutesAllBooks, GetTotalPages,
    // EndSession Exceptions, StartSessionAsync)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSessionAsync_ExistingSession_Removes()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        var session = await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow.AddMinutes(10),
            Minutes = 10
        });
        await _context.SaveChangesAsync();

        await _service.DeleteSessionAsync(session.Id);

        var reloaded = await _service.GetSessionByIdAsync(session.Id);
        reloaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSessionAsync_NonExisting_IsNoOp()
    {
        Func<Task> act = async () => await _service.DeleteSessionAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetSessionByIdAsync_Existing_ReturnsSession()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        var session = await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow.AddMinutes(5),
            Minutes = 5
        });
        await _context.SaveChangesAsync();

        var result = await _service.GetSessionByIdAsync(session.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task GetSessionByIdAsync_NonExisting_ReturnsNull()
    {
        var result = await _service.GetSessionByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSessionsByBookAsync_ReturnsOnlySessionsForThatBook()
    {
        var bookA = await _unitOfWork.Books.AddAsync(new Book { Title = "A", Author = "X" });
        var bookB = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "X" });
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _context.ReadingSessions.Add(new ReadingSession { BookId = bookA.Id, StartedAt = now, EndedAt = now.AddMinutes(5), Minutes = 5 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = bookA.Id, StartedAt = now, EndedAt = now.AddMinutes(10), Minutes = 10 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = bookB.Id, StartedAt = now, EndedAt = now.AddMinutes(3), Minutes = 3 });
        await _context.SaveChangesAsync();

        var result = await _service.GetSessionsByBookAsync(bookA.Id);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.BookId == bookA.Id);
    }

    [Fact]
    public async Task GetSessionsInRangeAsync_FiltersByDate()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = today.AddDays(-10), EndedAt = today.AddDays(-10).AddMinutes(5), Minutes = 5 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = today, EndedAt = today.AddMinutes(10), Minutes = 10 });
        await _context.SaveChangesAsync();

        var result = await _service.GetSessionsInRangeAsync(today.AddDays(-1), today.AddDays(1));

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRecentSessionsAsync_ReturnsLimitedCount()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        for (int i = 0; i < 5; i++)
        {
            _context.ReadingSessions.Add(new ReadingSession
            {
                BookId = book.Id,
                StartedAt = DateTime.UtcNow.AddMinutes(-i),
                EndedAt = DateTime.UtcNow.AddMinutes(-i + 1),
                Minutes = 1
            });
        }
        await _context.SaveChangesAsync();

        var result = await _service.GetRecentSessionsAsync(count: 3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTotalPagesAsync_SumsPages()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 10, PagesRead = 20 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 10, PagesRead = 30 });
        await _context.SaveChangesAsync();

        var total = await _service.GetTotalPagesAsync(book.Id);

        total.Should().Be(50);
    }

    [Fact]
    public async Task GetTotalMinutesAllBooksAsync_SumsAcrossBooks()
    {
        var bookA = await _unitOfWork.Books.AddAsync(new Book { Title = "A", Author = "X" });
        var bookB = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "X" });
        await _context.SaveChangesAsync();
        _context.ReadingSessions.Add(new ReadingSession { BookId = bookA.Id, StartedAt = DateTime.UtcNow, Minutes = 15 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = bookB.Id, StartedAt = DateTime.UtcNow, Minutes = 25 });
        await _context.SaveChangesAsync();

        var total = await _service.GetTotalMinutesAllBooksAsync();

        total.Should().Be(40);
    }

    [Fact]
    public async Task GetMinutesByDateAsync_GroupsByDate()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        var day1 = DateTime.UtcNow.Date.AddDays(-2);
        var day2 = DateTime.UtcNow.Date.AddDays(-1);
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = day1, EndedAt = day1.AddMinutes(30), Minutes = 30 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = day1, EndedAt = day1.AddMinutes(20), Minutes = 20 });
        _context.ReadingSessions.Add(new ReadingSession { BookId = book.Id, StartedAt = day2, EndedAt = day2.AddMinutes(15), Minutes = 15 });
        await _context.SaveChangesAsync();

        var result = await _service.GetMinutesByDateAsync(day1.AddDays(-1), day2.AddDays(1));

        result.Should().HaveCount(2);
        result[day1].Should().Be(50);
        result[day2].Should().Be(15);
    }

    [Fact]
    public async Task UpdateSessionAsync_PersistsChanges()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        var session = await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 5 });
        await _context.SaveChangesAsync();

        session.Minutes = 99;
        await _service.UpdateSessionAsync(session);

        var reloaded = await _service.GetSessionByIdAsync(session.Id);
        reloaded!.Minutes.Should().Be(99);
    }

    [Fact]
    public async Task EndSessionAsync_NonExistingSession_ThrowsEntityNotFound()
    {
        Func<Task> act = async () => await _service.EndSessionAsync(Guid.NewGuid(), 10);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task StartSessionAsync_CreatesOpenSession()
    {
        var book = new Book { Id = Guid.NewGuid(), Title = "B", Author = "A", Status = ReadingStatus.Reading };
        await _bookService.AddAsync(book);

        var session = await _service.StartSessionAsync(book.Id);

        session.Should().NotBeNull();
        session.BookId.Should().Be(book.Id);
        session.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        session.EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentStreakAsync_ReturnsCalculatedStreak()
    {
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "B", Author = "A" });
        await _context.SaveChangesAsync();
        var today = DateTime.UtcNow.Date;
        for (int i = 0; i < 3; i++)
        {
            _context.ReadingSessions.Add(new ReadingSession
            {
                BookId = book.Id,
                StartedAt = today.AddDays(-i).AddHours(12),
                EndedAt = today.AddDays(-i).AddHours(12).AddMinutes(20),
                Minutes = 20
            });
        }
        await _context.SaveChangesAsync();

        var streak = await _service.GetCurrentStreakAsync();

        streak.Should().BeGreaterThanOrEqualTo(1);
    }
}
