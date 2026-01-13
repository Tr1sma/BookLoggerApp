using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;
using Microsoft.EntityFrameworkCore;

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
    public async Task GetBooksCompletedInYearAsync_ShouldCountDatabaseSide_AndBeCorrect()
    {
        // Arrange
        var year = 2023;
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed 2023 1",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(year, 1, 1)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed 2023 2",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(year, 12, 31)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Completed 2022",
            Status = ReadingStatus.Completed,
            DateCompleted = new DateTime(year - 1, 1, 1)
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Reading",
            Status = ReadingStatus.Reading
        });
        await _context.SaveChangesAsync();

        // Act
        var count = await _service.GetBooksCompletedInYearAsync(year);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetAverageRatingByCategoryAsync_ShouldCalculateDatabaseSide_AndBeCorrect()
    {
        // Arrange
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 1",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow,
            CharactersRating = 4
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 2",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow,
            CharactersRating = 2
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 3",
            Status = ReadingStatus.Reading, // Should be ignored
            CharactersRating = 5
        });
        await _unitOfWork.Books.AddAsync(new Book
        {
            Title = "Book 4",
            Status = ReadingStatus.Completed, // No rating, should be ignored
            DateCompleted = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var average = await _service.GetAverageRatingByCategoryAsync(RatingCategory.Characters);

        // Assert
        average.Should().Be(3);
    }
}
