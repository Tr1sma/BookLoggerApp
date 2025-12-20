using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Integration;

/// <summary>
/// Tests to verify database migration from single Rating to multi-category ratings.
/// These tests simulate the migration scenario and verify backwards compatibility.
/// </summary>
public class MigrationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MockProgressionService _progressionService;
    private readonly MockPlantService _plantService;
    private readonly MockGoalService _goalService;
    private readonly BookService _bookService;

    public MigrationTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);

        _progressionService = new MockProgressionService();
        _plantService = new MockPlantService();
        _goalService = new MockGoalService();
        _bookService = new BookService(_unitOfWork, _progressionService, _plantService, _goalService, null!);
    }

    public void Dispose()
    {
        _context.Dispose();
    }





    [Fact]
    public async Task Migration_NewBooksWithMultipleRatings_ShouldWorkCorrectly()
    {
        // Arrange - Create a book with new rating system
        var newBook = new Book
        {
            Title = "New Book",
            Author = "New Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 5,
            SpiceLevelRating = 3,
            PacingRating = 4,
            WorldBuildingRating = 5
        };

        // Act
        var savedBook = await _bookService.AddAsync(newBook);
        var retrievedBook = await _bookService.GetByIdAsync(savedBook.Id);

        // Assert - All ratings should be saved and retrieved correctly
        retrievedBook.Should().NotBeNull();
        retrievedBook!.CharactersRating.Should().Be(5);
        retrievedBook.PlotRating.Should().Be(4);
        retrievedBook.WritingStyleRating.Should().Be(5);
        retrievedBook.SpiceLevelRating.Should().Be(3);
        retrievedBook.PacingRating.Should().Be(4);
        retrievedBook.WorldBuildingRating.Should().Be(5);

        // AverageRating should calculate correctly
        retrievedBook.AverageRating.Should().BeApproximately(4.33, 0.01);
    }





    [Fact]
    public async Task Migration_NullRatings_ShouldBeHandledCorrectly()
    {
        // Arrange - Create books with various null rating scenarios
        var noRatingsBook = new Book
        {
            Title = "No Ratings",
            Author = "Author",
            Status = ReadingStatus.Completed
        };

        var partialRatingsBook = new Book
        {
            Title = "Partial Ratings",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5,
            PlotRating = null,
            WritingStyleRating = 4
        };

        // Act
        await _bookService.AddAsync(noRatingsBook);
        await _bookService.AddAsync(partialRatingsBook);

        var retrievedNoRatings = await _bookService.GetByIdAsync(noRatingsBook.Id);
        var retrievedPartial = await _bookService.GetByIdAsync(partialRatingsBook.Id);

        // Assert
        retrievedNoRatings.Should().NotBeNull();
        retrievedNoRatings!.AverageRating.Should().BeNull();

        retrievedPartial.Should().NotBeNull();
        retrievedPartial!.AverageRating.Should().BeApproximately(4.5, 0.01); // (5 + 4) / 2
    }


}
