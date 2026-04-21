using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Models;

public class BookRatingSummaryTests
{
    [Fact]
    public void FromBook_WithRatings_PopulatesDictionary()
    {
        var book = new Book
        {
            Title = "T",
            CharactersRating = 5,
            PlotRating = 4,
            WritingStyleRating = 3,
            SpiceLevelRating = 2,
            PacingRating = 1,
            WorldBuildingRating = 5,
            SpannungRating = 4,
            HumorRating = 3,
            InformationsgehaltRating = 2,
            EmotionaleTiefeRating = 1,
            AtmosphaereRating = 5
        };

        var summary = BookRatingSummary.FromBook(book);

        summary.Book.Should().Be(book);
        summary.Ratings.Should().HaveCount(11);
        summary.Ratings[RatingCategory.Characters].Should().Be(5);
        summary.Ratings[RatingCategory.Atmosphaere].Should().Be(5);
        summary.AverageRating.Should().BeApproximately(book.AverageRating!.Value, 0.01);
    }

    [Fact]
    public void FromBook_NoRatings_AverageIsZero()
    {
        var book = new Book { Title = "T" };

        var summary = BookRatingSummary.FromBook(book);

        summary.AverageRating.Should().Be(0);
        summary.Ratings.Values.Should().AllSatisfy(v => v.Should().BeNull());
    }
}

public class OnboardingMissionCatalogTests
{
    [Fact]
    public void Missions_HasExpectedCount()
    {
        OnboardingMissionCatalog.Missions.Should().HaveCountGreaterThan(10);
    }

    [Fact]
    public void FeatureAtlas_IsNonEmpty()
    {
        OnboardingMissionCatalog.FeatureAtlas.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDefinition_KnownId_ReturnsMatchingDefinition()
    {
        var def = OnboardingMissionCatalog.GetDefinition(OnboardingMissionId.AddFirstBook);

        def.Should().NotBeNull();
        def.Id.Should().Be(OnboardingMissionId.AddFirstBook);
    }

    [Fact]
    public void GetDefinition_UnknownId_Throws()
    {
        Action act = () => OnboardingMissionCatalog.GetDefinition((OnboardingMissionId)9999);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CurrentFlowVersion_IsPositive()
    {
        OnboardingMissionCatalog.CurrentFlowVersion.Should().BeGreaterThan(0);
    }

    [Fact]
    public void IntroStepCount_IsPositive()
    {
        OnboardingMissionCatalog.IntroStepCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CoreMissions_AllExistInCatalog()
    {
        foreach (var missionId in Enum.GetValues<OnboardingMissionId>())
        {
            var def = OnboardingMissionCatalog.GetDefinition(missionId);
            def.Should().NotBeNull();
            def.Id.Should().Be(missionId);
        }
    }
}

public class TimerStateDataComputedTests
{
    [Fact]
    public void StartTimeUtc_ZeroTicks_IsMinValue()
    {
        var data = new BookLoggerApp.Core.Services.Abstractions.TimerStateData();

        data.StartTimeUtc.Should().Be(new DateTime(0, DateTimeKind.Utc));
    }
}
