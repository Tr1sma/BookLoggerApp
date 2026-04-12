using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class AdvancedStatsServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly AdvancedStatsService _service;

    public AdvancedStatsServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new AdvancedStatsService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ===== GetReadingHeatmapAsync =====

    [Fact]
    public async Task GetReadingHeatmapAsync_WithSessions_ShouldReturnMinutesPerDay()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 30
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, 20, 0, 0, DateTimeKind.Utc),
            Minutes = 20
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 16, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 45
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetReadingHeatmapAsync(2025);

        // Assert
        result.Should().HaveCount(2);
        result[new DateTime(2025, 3, 15)].Should().Be(50);
        result[new DateTime(2025, 3, 16)].Should().Be(45);
    }

    [Fact]
    public async Task GetReadingHeatmapAsync_WithNoSessions_ShouldReturnEmpty()
    {
        // Act
        var result = await _service.GetReadingHeatmapAsync(2025);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReadingHeatmapAsync_ShouldExcludeZeroMinuteSessions()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 0
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc),
            Minutes = 25
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetReadingHeatmapAsync(2025);

        // Assert
        result.Should().HaveCount(1);
        result[new DateTime(2025, 6, 1)].Should().Be(25);
    }

    [Fact]
    public async Task GetReadingHeatmapAsync_ShouldOnlyReturnSessionsFromRequestedYear()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 30
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 45
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetReadingHeatmapAsync(2025);

        // Assert
        result.Should().HaveCount(1);
        result[new DateTime(2025, 6, 1)].Should().Be(45);
    }

    // ===== GetWeekdayDistributionAsync =====

    [Fact]
    public async Task GetWeekdayDistributionAsync_WithSessions_ShouldSumMinutesByWeekday()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        // Monday 2025-03-17
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 17, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 30
        });
        // Another Monday
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 24, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 20
        });
        // Tuesday 2025-03-18
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 18, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 45
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetWeekdayDistributionAsync();

        // Assert
        result.Should().HaveCount(2);
        result[DayOfWeek.Monday].Should().Be(50);
        result[DayOfWeek.Tuesday].Should().Be(45);
    }

    [Fact]
    public async Task GetWeekdayDistributionAsync_WithNoSessions_ShouldReturnEmpty()
    {
        // Act
        var result = await _service.GetWeekdayDistributionAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWeekdayDistributionAsync_ShouldExcludeZeroMinuteSessions()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 17, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 0
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetWeekdayDistributionAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ===== GetTimeOfDayDistributionAsync =====

    [Fact]
    public async Task GetTimeOfDayDistributionAsync_ShouldCategorizeSessions()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        // Morning (hour 8)
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, 8, 0, 0, DateTimeKind.Utc),
            Minutes = 30
        });
        // Afternoon (hour 14)
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, 14, 0, 0, DateTimeKind.Utc),
            Minutes = 20
        });
        // Evening (hour 19)
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, 19, 0, 0, DateTimeKind.Utc),
            Minutes = 45
        });
        // Night (hour 23)
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, 23, 0, 0, DateTimeKind.Utc),
            Minutes = 15
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTimeOfDayDistributionAsync();

        // Assert
        result.Should().HaveCount(4);
        result["Morning"].Should().Be(30);
        result["Afternoon"].Should().Be(20);
        result["Evening"].Should().Be(45);
        result["Night"].Should().Be(15);
    }

    [Fact]
    public async Task GetTimeOfDayDistributionAsync_WithNoSessions_ShouldReturnAllKeysWithZero()
    {
        // Act
        var result = await _service.GetTimeOfDayDistributionAsync();

        // Assert
        result.Should().HaveCount(4);
        result["Morning"].Should().Be(0);
        result["Afternoon"].Should().Be(0);
        result["Evening"].Should().Be(0);
        result["Night"].Should().Be(0);
    }

    [Fact]
    public async Task GetTimeOfDayDistributionAsync_ShouldExcludeZeroMinuteSessions()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, 8, 0, 0, DateTimeKind.Utc),
            Minutes = 0
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc),
            Minutes = 25
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTimeOfDayDistributionAsync();

        // Assert
        result["Morning"].Should().Be(25);
    }

    [Theory]
    [InlineData(3, "Night")]   // 3 AM
    [InlineData(5, "Morning")]
    [InlineData(11, "Morning")]
    [InlineData(12, "Afternoon")]
    [InlineData(16, "Afternoon")]
    [InlineData(17, "Evening")]
    [InlineData(21, "Evening")]
    [InlineData(22, "Night")]
    [InlineData(0, "Night")]   // Midnight
    public async Task GetTimeOfDayDistributionAsync_ShouldRespectBucketBoundaries(int hour, string expectedBucket)
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = new DateTime(2025, 3, 15, hour, 0, 0, DateTimeKind.Utc),
            Minutes = 10
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTimeOfDayDistributionAsync();

        // Assert
        result[expectedBucket].Should().Be(10);
        result.Where(kvp => kvp.Key != expectedBucket).All(kvp => kvp.Value == 0).Should().BeTrue();
    }

    // ===== GetSessionLengthDistributionAsync =====

    [Fact]
    public async Task GetSessionLengthDistributionAsync_ShouldCategorizeByLength()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 10 });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 20 });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 45 });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 90 });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession { BookId = book.Id, StartedAt = DateTime.UtcNow, Minutes = 150 });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSessionLengthDistributionAsync();

        // Assert
        result.Should().HaveCount(5);
        result["<15"].Should().Be(1);
        result["15-30"].Should().Be(1);
        result["30-60"].Should().Be(1);
        result["1-2h"].Should().Be(1);
        result[">2h"].Should().Be(1);
    }

    [Fact]
    public async Task GetSessionLengthDistributionAsync_WithNoSessions_ShouldReturnAllKeysWithZero()
    {
        // Act
        var result = await _service.GetSessionLengthDistributionAsync();

        // Assert
        result.Should().HaveCount(5);
        result["<15"].Should().Be(0);
        result["15-30"].Should().Be(0);
        result["30-60"].Should().Be(0);
        result["1-2h"].Should().Be(0);
        result[">2h"].Should().Be(0);
    }

    [Theory]
    [InlineData(0, "<15")]
    [InlineData(14, "<15")]
    [InlineData(15, "15-30")]
    [InlineData(29, "15-30")]
    [InlineData(30, "30-60")]
    [InlineData(59, "30-60")]
    [InlineData(60, "1-2h")]
    [InlineData(119, "1-2h")]
    [InlineData(120, ">2h")]
    [InlineData(300, ">2h")]
    public async Task GetSessionLengthDistributionAsync_ShouldRespectBucketBoundaries(int minutes, string expectedBucket)
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow,
            Minutes = minutes
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSessionLengthDistributionAsync();

        // Assert
        result[expectedBucket].Should().Be(1);
        result.Where(kvp => kvp.Key != expectedBucket).All(kvp => kvp.Value == 0).Should().BeTrue();
    }

    // ===== GetMonthlyVolumeAsync =====

    [Fact]
    public async Task GetMonthlyVolumeAsync_WithCompletedBooks_ShouldCountPerMonth()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 1", Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2", Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 3", Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMonthlyVolumeAsync(2025);

        // Assert
        result.Should().HaveCount(2);
        result[3].Should().Be(2); // March
        result[6].Should().Be(1); // June
    }

    [Fact]
    public async Task GetMonthlyVolumeAsync_WithNoBooks_ShouldReturnEmpty()
    {
        // Act
        var result = await _service.GetMonthlyVolumeAsync(2025);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMonthlyVolumeAsync_ShouldExcludeNonCompletedBooks()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Reading", Author = "Author",
            Status = ReadingStatus.Reading,
            DateCompleted = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Planned", Author = "Author",
            Status = ReadingStatus.Planned
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed", Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMonthlyVolumeAsync(2025);

        // Assert
        result.Should().HaveCount(1);
        result[3].Should().Be(1);
    }

    [Fact]
    public async Task GetMonthlyVolumeAsync_ShouldOnlyReturnBooksFromRequestedYear()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2024", Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2025", Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMonthlyVolumeAsync(2025);

        // Assert
        result.Should().HaveCount(1);
        result[5].Should().Be(1);
    }

    // ===== GetReadingSpeedTrendAsync =====

    [Fact]
    public async Task GetReadingSpeedTrendAsync_WithSessions_ShouldCalculatePagesPerHour()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Current month: 60 pages in 60 minutes = 60 pages/hour
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = currentMonthStart.AddDays(1),
            Minutes = 60,
            PagesRead = 60
        });

        // Previous month: 30 pages in 60 minutes = 30 pages/hour
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = currentMonthStart.AddMonths(-1).AddDays(1),
            Minutes = 60,
            PagesRead = 30
        });
        await _context.SaveChangesAsync();

        // Act
        var (current, previous) = await _service.GetReadingSpeedTrendAsync();

        // Assert
        current.Should().Be(60);
        previous.Should().Be(30);
    }

    [Fact]
    public async Task GetReadingSpeedTrendAsync_WithNoSessions_ShouldReturnZeros()
    {
        // Act
        var (current, previous) = await _service.GetReadingSpeedTrendAsync();

        // Assert
        current.Should().Be(0);
        previous.Should().Be(0);
    }

    [Fact]
    public async Task GetReadingSpeedTrendAsync_ShouldExcludeSessionsWithZeroMinutes()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = currentMonthStart.AddDays(1),
            Minutes = 0,
            PagesRead = 50
        });
        await _context.SaveChangesAsync();

        // Act
        var (current, _) = await _service.GetReadingSpeedTrendAsync();

        // Assert
        current.Should().Be(0);
    }

    [Fact]
    public async Task GetReadingSpeedTrendAsync_ShouldExcludeSessionsWithoutPagesRead()
    {
        // Arrange
        var book = await _unitOfWork.Books.AddAsync(new Book { Title = "Test", Author = "Author" });
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = currentMonthStart.AddDays(1),
            Minutes = 30,
            PagesRead = null
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = currentMonthStart.AddDays(1),
            Minutes = 30,
            PagesRead = 0
        });
        await _context.SaveChangesAsync();

        // Act
        var (current, _) = await _service.GetReadingSpeedTrendAsync();

        // Assert
        current.Should().Be(0);
    }

    // ===== GetAverageFinishTimeTrendAsync =====

    [Fact]
    public async Task GetAverageFinishTimeTrendAsync_WithBooks_ShouldCalculateAverageDays()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Current period (last 30 days): book finished in 10 days
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Recent Book", Author = "Author",
            Status = ReadingStatus.Completed,
            DateStarted = now.AddDays(-15),
            DateCompleted = now.AddDays(-5)
        });

        // Previous period (30-60 days ago): book finished in 20 days
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Older Book", Author = "Author",
            Status = ReadingStatus.Completed,
            DateStarted = now.AddDays(-65),
            DateCompleted = now.AddDays(-45)
        });
        await _context.SaveChangesAsync();

        // Act
        var (currentAvg, previousAvg) = await _service.GetAverageFinishTimeTrendAsync();

        // Assert
        currentAvg.Should().Be(10.0);
        previousAvg.Should().Be(20.0);
    }

    [Fact]
    public async Task GetAverageFinishTimeTrendAsync_WithNoBooks_ShouldReturnZeros()
    {
        // Act
        var (currentAvg, previousAvg) = await _service.GetAverageFinishTimeTrendAsync();

        // Assert
        currentAvg.Should().Be(0);
        previousAvg.Should().Be(0);
    }

    [Fact]
    public async Task GetAverageFinishTimeTrendAsync_ShouldExcludeBooksWithoutDates()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Book without DateStarted
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "No Start", Author = "Author",
            Status = ReadingStatus.Completed,
            DateStarted = null,
            DateCompleted = now.AddDays(-5)
        });

        // Book without DateCompleted
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "No Completion", Author = "Author",
            Status = ReadingStatus.Completed,
            DateStarted = now.AddDays(-10),
            DateCompleted = null
        });
        await _context.SaveChangesAsync();

        // Act
        var (currentAvg, previousAvg) = await _service.GetAverageFinishTimeTrendAsync();

        // Assert
        currentAvg.Should().Be(0);
        previousAvg.Should().Be(0);
    }

    [Fact]
    public async Task GetAverageFinishTimeTrendAsync_ShouldOnlyIncludeCompletedBooks()
    {
        // Arrange
        var now = DateTime.UtcNow;

        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Reading", Author = "Author",
            Status = ReadingStatus.Reading,
            DateStarted = now.AddDays(-15),
            DateCompleted = now.AddDays(-5)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed", Author = "Author",
            Status = ReadingStatus.Completed,
            DateStarted = now.AddDays(-12),
            DateCompleted = now.AddDays(-5)
        });
        await _context.SaveChangesAsync();

        // Act
        var (currentAvg, _) = await _service.GetAverageFinishTimeTrendAsync();

        // Assert
        currentAvg.Should().Be(7.0);
    }

    // ===== GetYearComparisonAsync =====

    [Fact]
    public async Task GetYearComparisonAsync_ReturnsStatsForBothYears()
    {
        // Arrange
        var book2025 = await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "2025 Book", Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            PageCount = 300,
            CharactersRating = 5,
            PlotRating = 4
        });

        var book2026 = await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "2026 Book", Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            PageCount = 400,
            WritingStyleRating = 5
        });
        await _context.SaveChangesAsync();

        // Add sessions for 2025
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book2025.Id,
            StartedAt = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 120,
            PagesRead = 50
        });

        // Add sessions for 2026
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = book2026.Id,
            StartedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            Minutes = 180,
            PagesRead = 75
        });
        await _context.SaveChangesAsync();

        // Act
        var (stats2025, stats2026) = await _service.GetYearComparisonAsync(2025, 2026);

        // Assert
        stats2025.Year.Should().Be(2025);
        stats2025.BooksCompleted.Should().Be(1);
        stats2025.PagesRead.Should().Be(300);
        stats2025.MinutesRead.Should().Be(120);
        stats2025.AverageRating.Should().Be(4.5); // (5 + 4) / 2

        stats2026.Year.Should().Be(2026);
        stats2026.BooksCompleted.Should().Be(1);
        stats2026.PagesRead.Should().Be(400);
        stats2026.MinutesRead.Should().Be(180);
        stats2026.AverageRating.Should().Be(5.0); // Only WritingStyleRating = 5
    }

    // ===== GetGenreRadarDataAsync =====

    [Fact]
    public async Task GetGenreRadarDataAsync_ReturnsTopGenres()
    {
        // Arrange
        var fantasyGenre = await _unitOfWork.Genres.AddAsync(new Genre { Name = "Fantasy", Icon = "🧙" });
        var mysteryGenre = await _unitOfWork.Genres.AddAsync(new Genre { Name = "Mystery", Icon = "🔍" });
        var scifiGenre = await _unitOfWork.Genres.AddAsync(new Genre { Name = "Sci-Fi", Icon = "🚀" });
        await _context.SaveChangesAsync();

        var book1 = await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 1", Author = "Author",
            Status = ReadingStatus.Completed
        });
        var book2 = await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2", Author = "Author",
            Status = ReadingStatus.Completed
        });
        var book3 = await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 3", Author = "Author",
            Status = ReadingStatus.Completed
        });
        var uncompletedBook = await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Planned Book", Author = "Author",
            Status = ReadingStatus.Planned
        });
        await _context.SaveChangesAsync();

        // Book1: Fantasy, Mystery (2 genres, count as 1 book)
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book1.Id, GenreId = fantasyGenre.Id });
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book1.Id, GenreId = mysteryGenre.Id });

        // Book2: Fantasy, Sci-Fi
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book2.Id, GenreId = fantasyGenre.Id });
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book2.Id, GenreId = scifiGenre.Id });

        // Book3: Mystery
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = book3.Id, GenreId = mysteryGenre.Id });

        // UncompletedBook: Fantasy (should be excluded)
        await _unitOfWork.BookGenres.AddAsync(new BookGenre { BookId = uncompletedBook.Id, GenreId = fantasyGenre.Id });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetGenreRadarDataAsync(maxGenres: 8);

        // Assert
        result.Should().HaveCount(3);
        result["Fantasy"].Should().Be(2); // Book1, Book2
        result["Mystery"].Should().Be(2); // Book1, Book3
        result["Sci-Fi"].Should().Be(1);  // Book2
    }

    // ===== GetCompletionRateAsync =====

    [Fact]
    public async Task GetCompletionRateAsync_CountsCorrectly()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book { Title = "Completed 1", Author = "Author", Status = ReadingStatus.Completed });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Completed 2", Author = "Author", Status = ReadingStatus.Completed });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Abandoned 1", Author = "Author", Status = ReadingStatus.Abandoned });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Reading", Author = "Author", Status = ReadingStatus.Reading });
        await _unitOfWork.Books.AddAsync(new Book { Title = "Planned", Author = "Author", Status = ReadingStatus.Planned });
        await _context.SaveChangesAsync();

        // Act
        var (completed, abandoned) = await _service.GetCompletionRateAsync();

        // Assert
        completed.Should().Be(2);
        abandoned.Should().Be(1);
    }

    [Fact]
    public async Task GetCompletionRateAsync_EmptyData_ReturnsZeros()
    {
        // Act
        var (completed, abandoned) = await _service.GetCompletionRateAsync();

        // Assert
        completed.Should().Be(0);
        abandoned.Should().Be(0);
    }

    // ===== GetPageCountDistributionAsync =====

    [Fact]
    public async Task GetPageCountDistributionAsync_BucketsCorrectly()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Short", Author = "Author",
            Status = ReadingStatus.Completed,
            PageCount = 150
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Medium 1", Author = "Author",
            Status = ReadingStatus.Completed,
            PageCount = 350
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Medium 2", Author = "Author",
            Status = ReadingStatus.Completed,
            PageCount = 200
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Long 1", Author = "Author",
            Status = ReadingStatus.Completed,
            PageCount = 500
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Long 2", Author = "Author",
            Status = ReadingStatus.Completed,
            PageCount = 800
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPageCountDistributionAsync();

        // Assert
        result.Should().HaveCount(4);
        result["<200"].Should().Be(1);   // 150
        result["200-400"].Should().Be(2); // 350, 200
        result["400-600"].Should().Be(1); // 500
        result[">600"].Should().Be(1);    // 800
    }

    [Fact]
    public async Task GetPageCountDistributionAsync_AllKeysPresent_WhenEmpty()
    {
        // Act
        var result = await _service.GetPageCountDistributionAsync();

        // Assert
        result.Should().HaveCount(4);
        result["<200"].Should().Be(0);
        result["200-400"].Should().Be(0);
        result["400-600"].Should().Be(0);
        result[">600"].Should().Be(0);
    }

    // ===== GetTopAuthorsAsync =====

    [Fact]
    public async Task GetTopAuthorsAsync_RanksByBookCount()
    {
        // Arrange
        // Sanderson: 2 books, 800 pages total
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Mistborn 1", Author = "Brandon Sanderson",
            Status = ReadingStatus.Completed,
            PageCount = 400
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Mistborn 2", Author = "Brandon Sanderson",
            Status = ReadingStatus.Completed,
            PageCount = 400
        });

        // King: 1 book, 450 pages
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "The Stand", Author = "Stephen King",
            Status = ReadingStatus.Completed,
            PageCount = 450
        });

        // Martin: 2 books, 900 pages
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "AGOT", Author = "George R.R. Martin",
            Status = ReadingStatus.Completed,
            PageCount = 500
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "ACOK", Author = "George R.R. Martin",
            Status = ReadingStatus.Completed,
            PageCount = 400
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTopAuthorsAsync(count: 5);

        // Assert
        result.Should().HaveCount(3);
        result[0].Author.Should().Be("George R.R. Martin");
        result[0].BookCount.Should().Be(2);
        result[0].TotalPages.Should().Be(900);

        result[1].Author.Should().Be("Brandon Sanderson");
        result[1].BookCount.Should().Be(2);
        result[1].TotalPages.Should().Be(800);

        result[2].Author.Should().Be("Stephen King");
        result[2].BookCount.Should().Be(1);
        result[2].TotalPages.Should().Be(450);
    }

    [Fact]
    public async Task GetTopAuthorsAsync_RanksSecondaryByPages()
    {
        // Arrange
        // Author1: 1 book, 500 pages
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book A", Author = "Author1",
            Status = ReadingStatus.Completed,
            PageCount = 500
        });

        // Author2: 1 book, 300 pages
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book B", Author = "Author2",
            Status = ReadingStatus.Completed,
            PageCount = 300
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTopAuthorsAsync(count: 5);

        // Assert
        result.Should().HaveCount(2);
        result[0].Author.Should().Be("Author1");
        result[0].TotalPages.Should().Be(500);
        result[1].Author.Should().Be("Author2");
        result[1].TotalPages.Should().Be(300);
    }

    [Fact]
    public async Task GetTopAuthorsAsync_IgnoresNonCompletedBooks()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed", Author = "Active Author",
            Status = ReadingStatus.Completed,
            PageCount = 300
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Reading", Author = "Active Author",
            Status = ReadingStatus.Reading,
            PageCount = 200
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Planned", Author = "Inactive Author",
            Status = ReadingStatus.Planned,
            PageCount = 400
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTopAuthorsAsync(count: 5);

        // Assert
        result.Should().HaveCount(1);
        result[0].Author.Should().Be("Active Author");
        result[0].BookCount.Should().Be(1);
        result[0].TotalPages.Should().Be(300);
    }

    [Fact]
    public async Task GetTopAuthorsAsync_HandlesMissingPages()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "No Pages", Author = "Author1",
            Status = ReadingStatus.Completed,
            PageCount = null
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "With Pages", Author = "Author1",
            Status = ReadingStatus.Completed,
            PageCount = 300
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTopAuthorsAsync(count: 5);

        // Assert
        result.Should().HaveCount(1);
        result[0].Author.Should().Be("Author1");
        result[0].BookCount.Should().Be(2);
        result[0].TotalPages.Should().Be(300); // null treated as 0
    }

    [Fact]
    public async Task GetTopAuthorsAsync_RespectCountParameter()
    {
        // Arrange
        for (int i = 1; i <= 10; i++)
        {
            await _unitOfWork.Books.AddAsync(new Book
            {
                Title = $"Book {i}", Author = $"Author {i}",
                Status = ReadingStatus.Completed,
                PageCount = 100 * i
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTopAuthorsAsync(count: 3);

        // Assert
        result.Should().HaveCount(3);
    }
}
