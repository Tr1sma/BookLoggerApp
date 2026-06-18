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
        var goal = new ReadingGoal();

        goal.Id.Should().NotBeEmpty();
        goal.Current.Should().Be(0);
        goal.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void ProgressPercentage_ShouldCalculateCorrectly()
    {

        var goal = new ReadingGoal
        {
            Target = 100,
            Current = 25
        };


        var percentage = goal.ProgressPercentage;


        percentage.Should().Be(25);
    }

    [Fact]
    public void ProgressPercentage_WithZeroTarget_ShouldReturnZero()
    {

        var goal = new ReadingGoal
        {
            Target = 0,
            Current = 25
        };


        var percentage = goal.ProgressPercentage;


        percentage.Should().Be(0);
    }

    [Fact]
    public void IsActive_WhenNotCompletedAndNotExpired_ShouldReturnTrue()
    {

        var goal = new ReadingGoal
        {
            IsCompleted = false,
            EndDate = DateTime.UtcNow.AddDays(1)
        };


        var isActive = goal.IsActive;


        isActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenCompleted_ShouldReturnFalse()
    {

        var goal = new ReadingGoal
        {
            IsCompleted = true,
            EndDate = DateTime.UtcNow.AddDays(1)
        };


        var isActive = goal.IsActive;


        isActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenExpired_ShouldReturnFalse()
    {

        var goal = new ReadingGoal
        {
            IsCompleted = false,
            EndDate = DateTime.UtcNow.AddDays(-1)
        };


        var isActive = goal.IsActive;


        isActive.Should().BeFalse();
    }
}
