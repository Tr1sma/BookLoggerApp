using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Models;

public class MonthlyReadingStatsTests
{
    [Theory]
    [InlineData(1, "Januar")]
    [InlineData(2, "Februar")]
    [InlineData(3, "März")]
    [InlineData(4, "April")]
    [InlineData(5, "Mai")]
    [InlineData(6, "Juni")]
    [InlineData(7, "Juli")]
    [InlineData(8, "August")]
    [InlineData(9, "September")]
    [InlineData(10, "Oktober")]
    [InlineData(11, "November")]
    [InlineData(12, "Dezember")]
    public void MonthNameGerman_Should_ReturnCorrectGermanName(int month, string expectedName)
    {
        // Arrange
        var stats = new MonthlyReadingStats { Month = month };

        // Act & Assert
        stats.MonthNameGerman.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void MonthNameGerman_Should_ReturnQuestionMark_ForInvalidMonth(int month)
    {
        // Arrange
        var stats = new MonthlyReadingStats { Month = month };

        // Act & Assert
        stats.MonthNameGerman.Should().Be("?");
    }
}
