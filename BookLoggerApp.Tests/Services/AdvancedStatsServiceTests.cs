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

    // ===== Analysen stubs =====

    [Fact]
    public async Task GetYearComparisonAsync_ShouldThrowNotImplemented()
    {
        // Act
        var act = () => _service.GetYearComparisonAsync(2024, 2025);

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task GetGenreRadarDataAsync_ShouldThrowNotImplemented()
    {
        // Act
        var act = () => _service.GetGenreRadarDataAsync();

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task GetCompletionRateAsync_ShouldThrowNotImplemented()
    {
        // Act
        var act = () => _service.GetCompletionRateAsync();

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task GetPageCountDistributionAsync_ShouldThrowNotImplemented()
    {
        // Act
        var act = () => _service.GetPageCountDistributionAsync();

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task GetTopAuthorsAsync_ShouldThrowNotImplemented()
    {
        // Act
        var act = () => _service.GetTopAuthorsAsync();

        // Assert
        await act.Should().ThrowAsync<NotImplementedException>();
    }
}
