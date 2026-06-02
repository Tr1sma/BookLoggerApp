using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Resources;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BookLoggerApp.Tests.Models;

public class SessionMoodInfoTests
{
    [Fact]
    public void GetAll_ReturnsOneEntryPerEnumValue()
    {
        var all = SessionMoodInfo.GetAll();

        all.Should().HaveCount(Enum.GetValues<SessionMood>().Length);
        all.Select(m => m.Mood).Should().BeEquivalentTo(Enum.GetValues<SessionMood>());
    }

    [Fact]
    public void GetAll_HasNonEmptyEmojiLabelAndDistinctColors()
    {
        var all = SessionMoodInfo.GetAll();

        all.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Emoji));
        all.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Label));
        all.Select(m => m.ColorHex).Distinct().Should().HaveCount(all.Count);
    }

    [Theory]
    [InlineData(SessionMood.Crying, "Crying")]
    [InlineData(SessionMood.Butterflies, "Butterflies")]
    [InlineData(SessionMood.Spice, "Spice")]
    [InlineData(SessionMood.Anger, "Anger")]
    [InlineData(SessionMood.Laughing, "Laughing")]
    [InlineData(SessionMood.MindBlown, "Mind-blown")]
    public void Get_WithLocalizer_ResolvesLabelFromResx(SessionMood mood, string expected)
    {
        IStringLocalizer<AppResources> localizer = new TestStringLocalizer<AppResources>();

        var info = SessionMoodInfo.Get(mood, localizer);

        info.Should().NotBeNull();
        info!.Label.Should().Be(expected);
    }
}
