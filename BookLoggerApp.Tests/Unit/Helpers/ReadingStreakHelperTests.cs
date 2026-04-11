using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class ReadingStreakHelperTests
{
    [Fact]
    public void CountsTowardStreak_ShouldReturnFalse_ForZeroProgressSession()
    {
        // Arrange
        var session = new ReadingSession
        {
            StartedAt = DateTime.UtcNow,
            Minutes = 0,
            PagesRead = 0,
            EndedAt = DateTime.UtcNow
        };

        // Act
        var countsTowardStreak = ReadingStreakHelper.CountsTowardStreak(session);

        // Assert
        countsTowardStreak.Should().BeFalse();
    }

    [Fact]
    public void CalculateCurrentStreak_ShouldIgnoreOpenPlaceholderSessions()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var sessions = new[]
        {
            new ReadingSession { StartedAt = today, Minutes = 15 },
            new ReadingSession { StartedAt = today.AddDays(-1), Minutes = 0, PagesRead = 0, EndedAt = null },
            new ReadingSession { StartedAt = today.AddDays(-2), Minutes = 15 }
        };

        // Act
        var streak = ReadingStreakHelper.CalculateCurrentStreak(sessions, today);

        // Assert
        streak.Should().Be(1);
    }

    [Fact]
    public void CalculateInclusiveStreak_ShouldIncludeCurrentSessionDate()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var sessions = new[]
        {
            new ReadingSession { StartedAt = today.AddDays(-1), Minutes = 15 },
            new ReadingSession { StartedAt = today.AddDays(-2), Minutes = 15 }
        };

        // Act
        var streak = ReadingStreakHelper.CalculateInclusiveStreak(sessions, today);

        // Assert
        streak.Should().Be(3);
    }

    [Fact]
    public void CalculateLongestStreak_ShouldIgnoreOpenPlaceholderSessions()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var sessions = new[]
        {
            new ReadingSession { StartedAt = today.AddDays(-5), Minutes = 10 },
            new ReadingSession { StartedAt = today.AddDays(-4), Minutes = 10 },
            new ReadingSession { StartedAt = today.AddDays(-3), Minutes = 0, PagesRead = 0, EndedAt = null },
            new ReadingSession { StartedAt = today.AddDays(-2), Minutes = 10 }
        };

        // Act
        var longestStreak = ReadingStreakHelper.CalculateLongestStreak(sessions);

        // Assert
        longestStreak.Should().Be(2);
    }
}
