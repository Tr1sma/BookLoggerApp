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
    private readonly BookService _bookService;

    public MigrationTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);

        _progressionService = new MockProgressionService();
        _plantService = new MockPlantService();
        _bookService = new BookService(_unitOfWork, _progressionService, _plantService, null!);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task Migration_OldBooksWithRating_ShouldHaveOverallRating()
    {
        // Arrange - Simulate a book created with old Rating property
        var oldBook = new Book
        {
            Title = "Old Book",
            Author = "Old Author",
            Status = ReadingStatus.Completed
        };

        // Set rating using obsolete property (simulating pre-migration data)
#pragma warning disable CS0618
        oldBook.Rating = 4;
#pragma warning restore CS0618

        // Act - Save the book
        var savedBook = await _bookService.AddAsync(oldBook);

        // Retrieve the book from database
        var retrievedBook = await _bookService.GetByIdAsync(savedBook.Id);

        // Assert - Rating should be in OverallRating
        retrievedBook.Should().NotBeNull();
        retrievedBook!.OverallRating.Should().Be(4);
#pragma warning disable CS0618
        retrievedBook.Rating.Should().Be(4);
#pragma warning restore CS0618

        // AverageRating should return OverallRating when no category ratings
        retrievedBook.AverageRating.Should().Be(4);
    }

    [Fact]
    public async Task Migration_OldBooksCanBeUpgradedToNewRatings()
    {
        // Arrange - Create a book with old-style rating
        var book = new Book
        {
            Title = "Upgrade Test",
            Author = "Author",
            Status = ReadingStatus.Completed
        };

#pragma warning disable CS0618
        book.Rating = 3;
#pragma warning restore CS0618

        book = await _bookService.AddAsync(book);

        // Act - Upgrade to new rating system by adding category ratings
        book.CharactersRating = 4;
        book.PlotRating = 5;
        book.WritingStyleRating = 4;
        // Keep OverallRating (which came from old Rating)

        await _bookService.UpdateAsync(book);

        // Retrieve the updated book
        var updatedBook = await _bookService.GetByIdAsync(book.Id);

        // Assert - Should have both old and new ratings
        updatedBook.Should().NotBeNull();
        updatedBook!.OverallRating.Should().Be(3); // Original rating preserved
        updatedBook.CharactersRating.Should().Be(4);
        updatedBook.PlotRating.Should().Be(5);
        updatedBook.WritingStyleRating.Should().Be(4);

        // AverageRating should now calculate from category ratings
        updatedBook.AverageRating.Should().BeApproximately(4.33, 0.01); // (4 + 5 + 4) / 3
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
            WorldBuildingRating = 5,
            OverallRating = 4
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
        retrievedBook.OverallRating.Should().Be(4);

        // AverageRating should calculate correctly
        retrievedBook.AverageRating.Should().BeApproximately(4.33, 0.01);
    }

    [Fact]
    public async Task Migration_MixedBooks_ShouldCoexist()
    {
        // Arrange - Create a mix of old and new style books
        var oldStyleBook = new Book
        {
            Title = "Old Style",
            Author = "Author",
            Status = ReadingStatus.Completed
        };
#pragma warning disable CS0618
        oldStyleBook.Rating = 3;
#pragma warning restore CS0618

        var newStyleBook = new Book
        {
            Title = "New Style",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 5,
            PlotRating = 5
        };

        var hybridBook = new Book
        {
            Title = "Hybrid Style",
            Author = "Author",
            Status = ReadingStatus.Completed,
            OverallRating = 4,
            CharactersRating = 5
        };

        // Act - Save all books
        await _bookService.AddAsync(oldStyleBook);
        await _bookService.AddAsync(newStyleBook);
        await _bookService.AddAsync(hybridBook);

        // Retrieve all completed books
        var allBooks = await _unitOfWork.Books.GetBooksByStatusAsync(ReadingStatus.Completed);

        // Assert - All books should be in database
        allBooks.Should().HaveCount(3);

        // Old style book
        var retrievedOld = allBooks.First(b => b.Title == "Old Style");
        retrievedOld.OverallRating.Should().Be(3);
        retrievedOld.AverageRating.Should().Be(3);

        // New style book
        var retrievedNew = allBooks.First(b => b.Title == "New Style");
        retrievedNew.CharactersRating.Should().Be(5);
        retrievedNew.PlotRating.Should().Be(5);
        retrievedNew.AverageRating.Should().Be(5.0);

        // Hybrid book
        var retrievedHybrid = allBooks.First(b => b.Title == "Hybrid Style");
        retrievedHybrid.OverallRating.Should().Be(4);
        retrievedHybrid.CharactersRating.Should().Be(5);
        retrievedHybrid.AverageRating.Should().Be(5.0); // Only Characters rating
    }

    [Fact]
    public async Task Migration_DatabaseSchema_ShouldHaveAllColumns()
    {
        // This test verifies that the database schema has all the required columns
        // after the migration

        // Arrange - Create a book with all rating fields populated
        var book = new Book
        {
            Title = "Schema Test",
            Author = "Author",
            Status = ReadingStatus.Completed,
            CharactersRating = 1,
            PlotRating = 2,
            WritingStyleRating = 3,
            SpiceLevelRating = 4,
            PacingRating = 5,
            WorldBuildingRating = 1,
            OverallRating = 2
        };

        // Act - Save and retrieve
        await _bookService.AddAsync(book);
        var retrieved = await _unitOfWork.Books.GetByIdAsync(book.Id);

        // Assert - All columns should be persisted
        retrieved.Should().NotBeNull();
        retrieved!.CharactersRating.Should().Be(1);
        retrieved.PlotRating.Should().Be(2);
        retrieved.WritingStyleRating.Should().Be(3);
        retrieved.SpiceLevelRating.Should().Be(4);
        retrieved.PacingRating.Should().Be(5);
        retrieved.WorldBuildingRating.Should().Be(1);
        retrieved.OverallRating.Should().Be(2);
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

    [Fact]
    public async Task Migration_ObsoleteRatingProperty_ShouldWorkThroughAllLayers()
    {
        // This test verifies that the obsolete Rating property works correctly
        // through all layers of the application (Model -> Repository -> Service)

        // Arrange
        var book = new Book
        {
            Title = "Obsolete Test",
            Author = "Author",
            Status = ReadingStatus.Completed
        };

        // Act - Set via obsolete property before saving
#pragma warning disable CS0618
        book.Rating = 5;
#pragma warning restore CS0618

        await _bookService.AddAsync(book);

        // Retrieve via service
        var retrieved = await _bookService.GetByIdAsync(book.Id);

        // Assert - Should work through all layers
        retrieved.Should().NotBeNull();
        retrieved!.OverallRating.Should().Be(5);
#pragma warning disable CS0618
        retrieved.Rating.Should().Be(5);
#pragma warning restore CS0618

        // Update via obsolete property
#pragma warning disable CS0618
        retrieved.Rating = 4;
#pragma warning restore CS0618

        await _bookService.UpdateAsync(retrieved);

        // Retrieve again
        var updatedBook = await _bookService.GetByIdAsync(book.Id);
        updatedBook!.OverallRating.Should().Be(4);
#pragma warning disable CS0618
        updatedBook.Rating.Should().Be(4);
#pragma warning restore CS0618
    }
}
