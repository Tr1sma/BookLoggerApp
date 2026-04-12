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
        // Act
        var result = GenreRatingMapping.GetRelevantCategories(new[] { RomanceId });

        // Assert
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
        // Act
        var result = GenreRatingMapping.GetRelevantCategories(new[] { RomanceId, ThrillerId });

        // Assert — union should include both Romance and Thriller categories
        result.Should().Contain(RatingCategory.SpiceLevel); // Romance
        result.Should().Contain(RatingCategory.EmotionaleTiefe); // Romance
        result.Should().Contain(RatingCategory.Spannung); // Thriller
        result.Should().Contain(RatingCategory.Atmosphaere); // Thriller
        result.Should().Contain(RatingCategory.Characters);
        result.Should().Contain(RatingCategory.Plot);
    }

    [Fact]
    public void GetRelevantCategories_WithNoGenres_ReturnsAllCategories()
    {
        // Act
        var result = GenreRatingMapping.GetRelevantCategories(Array.Empty<Guid>());

        // Assert
        result.Should().HaveCount(11);
    }

    [Fact]
    public void GetRelevantCategories_WithUnknownGenreId_ReturnsAllCategories()
    {
        // Act
        var result = GenreRatingMapping.GetRelevantCategories(new[] { Guid.NewGuid() });

        // Assert — unknown genre ID should trigger fallback to all categories
        result.Should().HaveCount(11);
    }

    [Fact]
    public void GetAdditionalCategories_ReturnsComplementOfRelevant()
    {
        // Act
        var relevant = GenreRatingMapping.GetRelevantCategories(new[] { NonFictionId });
        var additional = GenreRatingMapping.GetAdditionalCategories(new[] { NonFictionId });

        // Assert — Non-Fiction has 3 categories, so additional should have 8
        relevant.Should().HaveCount(3);
        additional.Should().HaveCount(8);

        // No overlap between relevant and additional
        foreach (RatingCategory cat in additional)
        {
            relevant.Should().NotContain(cat);
        }
    }

    [Fact]
    public void GetAdditionalCategories_WhenNoGenres_ReturnsEmpty()
    {
        // Act — no genres means all categories are relevant
        var additional = GenreRatingMapping.GetAdditionalCategories(Array.Empty<Guid>());

        // Assert
        additional.Should().BeEmpty();
    }

    [Fact]
    public void GetRelevantCategories_Fantasy_IncludesWorldBuildingAndAtmosphaere()
    {
        // Act
        var result = GenreRatingMapping.GetRelevantCategories(new[] { FantasyId });

        // Assert
        result.Should().Contain(RatingCategory.WorldBuilding);
        result.Should().Contain(RatingCategory.Atmosphaere);
        result.Should().NotContain(RatingCategory.SpiceLevel);
        result.Should().NotContain(RatingCategory.Spannung);
    }

    [Fact]
    public void GetRelevantCategories_Comedy_IncludesHumor()
    {
        // Act
        var result = GenreRatingMapping.GetRelevantCategories(new[] { ComedyId });

        // Assert
        result.Should().Contain(RatingCategory.Humor);
        result.Should().NotContain(RatingCategory.Spannung);
        result.Should().NotContain(RatingCategory.Atmosphaere);
    }
}
