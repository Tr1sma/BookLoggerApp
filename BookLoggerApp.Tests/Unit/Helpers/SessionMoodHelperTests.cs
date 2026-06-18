using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class SessionMoodHelperTests
{
    [Fact]
    public void Clamp_Null_ReturnsEmpty()
        => SessionMoodHelper.Clamp(null).Should().BeEmpty();

    [Fact]
    public void Clamp_Empty_ReturnsEmpty()
        => SessionMoodHelper.Clamp(Array.Empty<SessionMood>()).Should().BeEmpty();

    [Fact]
    public void Clamp_RemovesDuplicates()
    {
        var result = SessionMoodHelper.Clamp(new[] { SessionMood.Crying, SessionMood.Crying, SessionMood.Spice });
        result.Should().Equal(SessionMood.Crying, SessionMood.Spice);
    }

    [Fact]
    public void Clamp_LimitsToMax()
    {
        var result = SessionMoodHelper.Clamp(new[]
        {
            SessionMood.Crying, SessionMood.Butterflies, SessionMood.Spice, SessionMood.Anger
        });

        result.Should().HaveCount(SessionMoodHelper.MaxMoodsPerSession);
        result.Should().Equal(SessionMood.Crying, SessionMood.Butterflies, SessionMood.Spice);
    }

    [Fact]
    public void Clamp_PreservesOrder()
    {
        var result = SessionMoodHelper.Clamp(new[] { SessionMood.Anger, SessionMood.Crying });
        result.Should().Equal(SessionMood.Anger, SessionMood.Crying);
    }
}
