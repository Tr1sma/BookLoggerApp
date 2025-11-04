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
}
