using System;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class GoalActivityHelperTests
{
    private static ReadingGoal Goal(DateTime endDate, bool completed = false) => new()
    {
        Title = "g",
        StartDate = endDate.AddDays(-7),
        EndDate = endDate,
        IsCompleted = completed
    };

    [Fact]
    public void IsActiveAsOf_EndDateInFuture_ReturnsTrue()
    {
        var asOf = new DateTime(2025, 6, 10, 9, 0, 0);

        GoalActivityHelper.IsActiveAsOf(Goal(new DateTime(2025, 6, 20)), asOf).Should().BeTrue();
    }

    [Fact]
    public void IsActiveAsOf_EndDateIsTodayLateInDay_StillActive()
    {
        // The goal's last day: EndDate is local midnight, "now" is 23:00 the same local day.
        // A DateTime.UtcNow / now-instant comparison would have dropped it hours ago — the
        // local-midnight cutoff must keep it active for the whole final day (INK-06).
        var endDate = new DateTime(2025, 6, 10);
        var asOf = new DateTime(2025, 6, 10, 23, 0, 0);

        GoalActivityHelper.IsActiveAsOf(Goal(endDate), asOf).Should().BeTrue();
    }

    [Fact]
    public void IsActiveAsOf_EndDateYesterday_ReturnsFalse()
    {
        var asOf = new DateTime(2025, 6, 10, 1, 0, 0);

        GoalActivityHelper.IsActiveAsOf(Goal(new DateTime(2025, 6, 9)), asOf).Should().BeFalse();
    }

    [Fact]
    public void IsActiveAsOf_CompletedGoal_ReturnsFalse()
    {
        var asOf = new DateTime(2025, 6, 10);

        GoalActivityHelper.IsActiveAsOf(Goal(new DateTime(2025, 6, 20), completed: true), asOf)
            .Should().BeFalse();
    }

    [Fact]
    public void ActiveCutoff_StripsTimeOfDay()
    {
        var asOf = new DateTime(2025, 6, 10, 23, 59, 59);

        GoalActivityHelper.ActiveCutoff(asOf).Should().Be(new DateTime(2025, 6, 10));
    }
}
