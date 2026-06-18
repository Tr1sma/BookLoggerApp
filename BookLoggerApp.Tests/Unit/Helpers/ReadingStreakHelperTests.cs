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
        var session = new ReadingSession
        {
            StartedAt = DateTime.UtcNow,
            Minutes = 0,
            PagesRead = 0,
            EndedAt = DateTime.UtcNow
        };

        ReadingStreakHelper.CountsTowardStreak(session).Should().BeFalse();
    }

    [Fact]
    public void CalculateCurrentStreak_ShouldIgnoreOpenPlaceholderSessions()
    {
        var today = DateTime.UtcNow.Date;
        var sessions = new[]
        {
            new ReadingSession { StartedAt = today, Minutes = 15 },
            new ReadingSession { StartedAt = today.AddDays(-1), Minutes = 0, PagesRead = 0, EndedAt = null },
            new ReadingSession { StartedAt = today.AddDays(-2), Minutes = 15 }
        };

        ReadingStreakHelper.CalculateCurrentStreak(sessions, today).Should().Be(1);
    }

    [Fact]
    public void CalculateInclusiveStreak_ShouldIncludeCurrentSessionDate()
    {
        var today = DateTime.UtcNow.Date;
        var sessions = new[]
        {
            new ReadingSession { StartedAt = today.AddDays(-1), Minutes = 15 },
            new ReadingSession { StartedAt = today.AddDays(-2), Minutes = 15 }
        };

        ReadingStreakHelper.CalculateInclusiveStreak(sessions, today).Should().Be(3);
    }

    [Fact]
    public void CalculateLongestStreak_ShouldIgnoreOpenPlaceholderSessions()
    {
        var today = DateTime.UtcNow.Date;
        var sessions = new[]
        {
            new ReadingSession { StartedAt = today.AddDays(-5), Minutes = 10 },
            new ReadingSession { StartedAt = today.AddDays(-4), Minutes = 10 },
            new ReadingSession { StartedAt = today.AddDays(-3), Minutes = 0, PagesRead = 0, EndedAt = null },
            new ReadingSession { StartedAt = today.AddDays(-2), Minutes = 10 }
        };

        ReadingStreakHelper.CalculateLongestStreak(sessions).Should().Be(2);
    }
}
