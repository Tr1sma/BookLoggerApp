using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Models;

public class BookTests
{
    [Fact]
    public void Book_Constructor_ShouldSetDefaultValues()
    {
        var book = new Book();

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

        var book = new Book
        {
            PageCount = 100,
            CurrentPage = 25
        };


        var percentage = book.ProgressPercentage;


        percentage.Should().Be(25);
    }

    [Fact]
    public void ProgressPercentage_WithoutPageCount_ShouldReturnZero()
    {

        var book = new Book
        {
            PageCount = null,
            CurrentPage = 25
        };


        var percentage = book.ProgressPercentage;


        percentage.Should().Be(0);
    }

    [Fact]
    public void ProgressPercentage_WithZeroPageCount_ShouldReturnZero()
    {

        var book = new Book
        {
            PageCount = 0,
            CurrentPage = 25
        };


        var percentage = book.ProgressPercentage;


        percentage.Should().Be(0);
    }

    [Fact]
    public void ProgressPercentage_CurrentPageExceedsPageCount_ClampedTo100()
    {
        // PageCount typo fix must not produce >100%
        var book = new Book
        {
            PageCount = 100,
            CurrentPage = 150
        };


        var percentage = book.ProgressPercentage;


        percentage.Should().Be(100);
    }

    [Fact]
    public void ProgressPercentage_NegativeCurrentPage_ClampedToZero()
    {
        // clamp guarantees valid display
        var book = new Book
        {
            PageCount = 100,
            CurrentPage = -10
        };


        var percentage = book.ProgressPercentage;


        percentage.Should().Be(0);
    }

    #region Multi-Category Rating Tests

    [Fact]
    public void AverageRating_WithMultipleRatings_ShouldCalculateCorrectly()
    {

        var book = new Book
        {
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 5
        };


        var average = book.AverageRating;


        average.Should().NotBeNull();
        average.Value.Should().BeApproximately(4.67, 0.01);
    }

    [Fact]
    public void AverageRating_WithAllRatings_ShouldCalculateCorrectly()
    {

        var book = new Book
        {
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 5,
            SpiceLevelRating = 3,
            PacingRating = 4,
            WorldBuildingRating = 5
        };


        var average = book.AverageRating;


        average.Should().NotBeNull();
        average.Value.Should().BeApproximately(4.33, 0.01);
    }



    [Fact]
    public void AverageRating_WithNoRatings_ShouldReturnNull()
    {

        var book = new Book();


        var average = book.AverageRating;


        average.Should().BeNull();
    }

    [Fact]
    public void AverageRating_WithSomeNullRatings_ShouldIgnoreNulls()
    {

        var book = new Book
        {
            CharactersRating = 5,
            PlotRating = null,
            WritingStyleRating = 3,
            SpiceLevelRating = null,
            PacingRating = 4,
            WorldBuildingRating = null
        };


        var average = book.AverageRating;


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

        var book = new Book
        {
            CharactersRating = characters,
            PlotRating = plot,
            WritingStyleRating = writing,
            SpiceLevelRating = spice,
            PacingRating = pacing,
            WorldBuildingRating = world
        };


        var average = book.AverageRating;


        average.Should().NotBeNull();
        average.Value.Should().BeApproximately(expectedAverage, 0.01);
    }

    #endregion
}
