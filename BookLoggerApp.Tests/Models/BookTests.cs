using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Models;

public class BookTests
{
    [Fact]
    public void Book_Constructor_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var book = new Book();

        // Assert
        book.Id.Should().NotBeEmpty();
        book.Title.Should().BeEmpty();
        book.Author.Should().BeEmpty();
        book.CurrentPage.Should().Be(0);
        book.Status.Should().Be(ReadingStatus.Planned);
        book.DateAdded.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        book.BookGenres.Should().BeEmpty();
        book.ReadingSessions.Should().BeEmpty();
        book.Quotes.Should().BeEmpty();
        book.Annotations.Should().BeEmpty();
    }

    [Fact]
    public void ProgressPercentage_WithPageCount_ShouldCalculateCorrectly()
    {
        // Arrange
        var book = new Book
        {
            PageCount = 100,
            CurrentPage = 25
        };

        // Act
        var percentage = book.ProgressPercentage;

        // Assert
        percentage.Should().Be(25);
    }

    [Fact]
    public void ProgressPercentage_WithoutPageCount_ShouldReturnZero()
    {
        // Arrange
        var book = new Book
        {
            PageCount = null,
            CurrentPage = 25
        };

        // Act
        var percentage = book.ProgressPercentage;

        // Assert
        percentage.Should().Be(0);
    }

    [Fact]
    public void ProgressPercentage_WithZeroPageCount_ShouldReturnZero()
    {
        // Arrange
        var book = new Book
        {
            PageCount = 0,
            CurrentPage = 25
        };

        // Act
        var percentage = book.ProgressPercentage;

        // Assert
        percentage.Should().Be(0);
    }

    #region Multi-Category Rating Tests

    [Fact]
    public void AverageRating_WithMultipleRatings_ShouldCalculateCorrectly()
    {
        // Arrange
        var book = new Book
        {
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 5
        };

        // Act
        var average = book.AverageRating;

        // Assert
        average.Should().NotBeNull();
        average.Value.Should().BeApproximately(4.67, 0.01);
    }

    [Fact]
    public void AverageRating_WithAllRatings_ShouldCalculateCorrectly()
    {
        // Arrange
        var book = new Book
        {
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 5,
            SpiceLevelRating = 3,
            PacingRating = 4,
            WorldBuildingRating = 5
        };

        // Act
        var average = book.AverageRating;

        // Assert
        average.Should().NotBeNull();
        average.Value.Should().BeApproximately(4.33, 0.01);
    }



    [Fact]
    public void AverageRating_WithNoRatings_ShouldReturnNull()
    {
        // Arrange
        var book = new Book();

        // Act
        var average = book.AverageRating;

        // Assert
        average.Should().BeNull();
    }

    [Fact]
    public void AverageRating_WithSomeNullRatings_ShouldIgnoreNulls()
    {
        // Arrange
        var book = new Book
        {
            CharactersRating = 5,
            PlotRating = null,
            WritingStyleRating = 3,
            SpiceLevelRating = null,
            PacingRating = 4,
            WorldBuildingRating = null
        };

        // Act
        var average = book.AverageRating;

        // Assert
        average.Should().NotBeNull();
        average.Value.Should().BeApproximately(4.0, 0.01); // (5 + 3 + 4) / 3
    }



    [Theory]
    [InlineData(1, 2, 3, 4, 5, 5, 3.33)]
    [InlineData(5, 5, 5, 5, 5, 5, 5.0)]
    [InlineData(1, 1, 1, 1, 1, 1, 1.0)]
    [InlineData(3, 4, 5, 2, 3, 4, 3.5)]
    public void AverageRating_WithVariousRatings_ShouldCalculateCorrectly(
        int characters, int plot, int writing, int spice, int pacing, int world, double expectedAverage)
    {
        // Arrange
        var book = new Book
        {
            CharactersRating = characters,
            PlotRating = plot,
            WritingStyleRating = writing,
            SpiceLevelRating = spice,
            PacingRating = pacing,
            WorldBuildingRating = world
        };

        // Act
        var average = book.AverageRating;

        // Assert
        average.Should().NotBeNull();
        average.Value.Should().BeApproximately(expectedAverage, 0.01);
    }

    #endregion
}
