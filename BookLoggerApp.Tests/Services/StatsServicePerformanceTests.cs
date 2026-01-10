using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BookLoggerApp.Tests.Services;

public class StatsServicePerformanceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StatsService _service;

    public StatsServicePerformanceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new StatsService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetTotalMinutesReadAsync_ShouldCalculateSumDatabaseSide_AndBeCorrect()
    {
        // Arrange
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            Minutes = 60,
            StartedAt = DateTime.UtcNow
        });
        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            Minutes = 30,
            StartedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var totalMinutes = await _service.GetTotalMinutesReadAsync();

        // Assert
        totalMinutes.Should().Be(90);
    }

    [Fact]
    public async Task GetTotalPagesReadAsync_ShouldCalculateSumDatabaseSide_AndBeCorrect()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 1",
            Status = ReadingStatus.Completed,
            PageCount = 100
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2",
            Status = ReadingStatus.Completed,
            PageCount = 200
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 3",
            Status = ReadingStatus.Reading, // Should be ignored
            PageCount = 50
        });
        await _context.SaveChangesAsync();

        // Act
        var totalPages = await _service.GetTotalPagesReadAsync();

        // Assert
        totalPages.Should().Be(300);
    }

    [Fact]
    public async Task GetAverageRatingByCategoryAsync_ShouldBePerformant()
    {
        // Arrange
        // Add 1000 completed books
        var books = new List<Book>();
        var random = new Random();
        for (int i = 0; i < 1000; i++)
        {
            books.Add(new Book
            {
                Title = $"Book {i}",
                Status = ReadingStatus.Completed,
                DateCompleted = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                CharactersRating = random.Next(1, 6),
                PlotRating = random.Next(1, 6),
                WritingStyleRating = random.Next(1, 6),
                SpiceLevelRating = random.Next(1, 6),
                PacingRating = random.Next(1, 6),
                WorldBuildingRating = random.Next(1, 6)
            });
        }
        await _context.Books.AddRangeAsync(books);
        await _context.SaveChangesAsync();

        // Act
        var sw = Stopwatch.StartNew();
        var avg = await _service.GetAverageRatingByCategoryAsync(RatingCategory.Characters);
        sw.Stop();

        // Assert
        avg.Should().BeGreaterThan(0);
        // Asserting time in unit tests is flaky, but we can inspect the value manually
        // or check for query execution count if we had a profiler.
        // For now, we mainly want to ensure it still works correctly after optimization.
    }

    [Fact]
    public async Task GetAllAverageRatingsAsync_ShouldBePerformant()
    {
        // Arrange
        // Add 500 completed books
        var books = new List<Book>();
        var random = new Random();
        for (int i = 0; i < 500; i++)
        {
            books.Add(new Book
            {
                Title = $"Book {i}",
                Status = ReadingStatus.Completed,
                DateCompleted = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                CharactersRating = random.Next(1, 6),
                PlotRating = random.Next(1, 6),
                WritingStyleRating = random.Next(1, 6),
                SpiceLevelRating = random.Next(1, 6),
                PacingRating = random.Next(1, 6),
                WorldBuildingRating = random.Next(1, 6)
            });
        }
        await _context.Books.AddRangeAsync(books);
        await _context.SaveChangesAsync();

        // Act
        var sw = Stopwatch.StartNew();
        var result = await _service.GetAllAverageRatingsAsync();
        sw.Stop();

        // Assert
        result.Should().HaveCount(6);
        result[RatingCategory.Characters].Should().BeGreaterThan(0);

        // This is where we would fail if the implementation was slow, but since it's in-memory DB,
        // it's fast regardless. The real value is in checking the code change.
    }

    [Fact]
    public async Task GetBooksCompletedInYearAsync_ShouldUseDatabaseAggregation()
    {
        // Arrange
        var year = 2023;
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 1",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(year, 1, 1)
        });
         await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(year, 12, 31)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 3",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(year - 1, 12, 31)
        });
        await _context.SaveChangesAsync();

        // Act
        var count = await _service.GetBooksCompletedInYearAsync(year);

        // Assert
        count.Should().Be(2);
    }
}
