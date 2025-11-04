using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Models;

public class ReadingGoalTests
{
    [Fact]
    public void ReadingGoal_Constructor_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var goal = new ReadingGoal();

        // Assert
        goal.Id.Should().NotBeEmpty();
        goal.Current.Should().Be(0);
        goal.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void ProgressPercentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var goal = new ReadingGoal
        {
            Target = 100,
            Current = 25
        };

        // Act
        var percentage = goal.ProgressPercentage;

        // Assert
        percentage.Should().Be(25);
    }

    [Fact]
    public void ProgressPercentage_WithZeroTarget_ShouldReturnZero()
    {
        // Arrange
        var goal = new ReadingGoal
        {
            Target = 0,
            Current = 25
        };

        // Act
        var percentage = goal.ProgressPercentage;

        // Assert
        percentage.Should().Be(0);
    }

    [Fact]
    public void IsActive_WhenNotCompletedAndNotExpired_ShouldReturnTrue()
    {
        // Arrange
        var goal = new ReadingGoal
        {
            IsCompleted = false,
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var isActive = goal.IsActive;

        // Assert
        isActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenCompleted_ShouldReturnFalse()
    {
        // Arrange
        var goal = new ReadingGoal
        {
            IsCompleted = true,
            EndDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var isActive = goal.IsActive;

        // Assert
        isActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var goal = new ReadingGoal
        {
            IsCompleted = false,
            EndDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var isActive = goal.IsActive;

        // Assert
        isActive.Should().BeFalse();
    }
}
