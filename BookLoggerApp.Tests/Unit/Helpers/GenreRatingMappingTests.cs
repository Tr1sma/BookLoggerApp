using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class GenreRatingMappingTests
{
    private static readonly Guid RomanceId = Guid.Parse("00000000-0000-0000-0000-000000000006");
    private static readonly Guid FantasyId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid ThrillerId = Guid.Parse("00000000-0000-0000-0000-000000000012");
    private static readonly Guid NonFictionId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid ComedyId = Guid.Parse("00000000-0000-0000-0000-000000000011");

    [Fact]
    public void GetRelevantCategories_WithRomanceGenre_ReturnsExpectedCategories()
    {
        var result = GenreRatingMapping.GetRelevantCategories(new[] { RomanceId });

        result.Should().Contain(RatingCategory.Characters);
        result.Should().Contain(RatingCategory.Plot);
        result.Should().Contain(RatingCategory.WritingStyle);
        result.Should().Contain(RatingCategory.SpiceLevel);
        result.Should().Contain(RatingCategory.Pacing);
        result.Should().Contain(RatingCategory.EmotionaleTiefe);
        result.Should().NotContain(RatingCategory.WorldBuilding);
        result.Should().NotContain(RatingCategory.Spannung);
        result.Should().NotContain(RatingCategory.Humor);
    }

    [Fact]
    public void GetRelevantCategories_WithMultipleGenres_ReturnsUnion()
    {
        var result = GenreRatingMapping.GetRelevantCategories(new[] { RomanceId, ThrillerId });

        result.Should().Contain(RatingCategory.SpiceLevel); // Romance-only
        result.Should().Contain(RatingCategory.EmotionaleTiefe); // Romance-only
        result.Should().Contain(RatingCategory.Spannung); // Thriller-only
        result.Should().Contain(RatingCategory.Atmosphaere); // Thriller-only
        result.Should().Contain(RatingCategory.Characters);
        result.Should().Contain(RatingCategory.Plot);
    }

    [Fact]
    public void GetRelevantCategories_WithNoGenres_ReturnsAllCategories()
    {
        var result = GenreRatingMapping.GetRelevantCategories(Array.Empty<Guid>());

        result.Should().HaveCount(11);
    }

    [Fact]
    public void GetRelevantCategories_WithUnknownGenreId_ReturnsAllCategories()
    {
        var result = GenreRatingMapping.GetRelevantCategories(new[] { Guid.NewGuid() });

        result.Should().HaveCount(11);
    }

    [Fact]
    public void GetAdditionalCategories_ReturnsComplementOfRelevant()
    {
        var relevant = GenreRatingMapping.GetRelevantCategories(new[] { NonFictionId });
        var additional = GenreRatingMapping.GetAdditionalCategories(new[] { NonFictionId });

        // Non-Fiction: 3 relevant, 8 additional
        relevant.Should().HaveCount(3);
        additional.Should().HaveCount(8);

        foreach (RatingCategory cat in additional)
        {
            relevant.Should().NotContain(cat);
        }
    }

    [Fact]
    public void GetAdditionalCategories_WhenNoGenres_ReturnsEmpty()
    {
        var additional = GenreRatingMapping.GetAdditionalCategories(Array.Empty<Guid>());

        additional.Should().BeEmpty();
    }

    [Fact]
    public void GetRelevantCategories_Fantasy_IncludesWorldBuildingAndAtmosphaere()
    {
        var result = GenreRatingMapping.GetRelevantCategories(new[] { FantasyId });

        result.Should().Contain(RatingCategory.WorldBuilding);
        result.Should().Contain(RatingCategory.Atmosphaere);
        result.Should().NotContain(RatingCategory.SpiceLevel);
        result.Should().NotContain(RatingCategory.Spannung);
    }

    [Fact]
    public void GetRelevantCategories_Comedy_IncludesHumor()
    {
        var result = GenreRatingMapping.GetRelevantCategories(new[] { ComedyId });

        result.Should().Contain(RatingCategory.Humor);
        result.Should().NotContain(RatingCategory.Spannung);
        result.Should().NotContain(RatingCategory.Atmosphaere);
    }
}
