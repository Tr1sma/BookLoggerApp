using BookLoggerApp.Core.Services.Abstractions;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Services.Abstractions;

public class TimerStateDataTests
{
    [Fact]
    public void StartTimeUtc_DerivedFromTicks_IsUtc()
    {
        var expected = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var data = new TimerStateData { StartTimeTicks = expected.Ticks };

        data.StartTimeUtc.Should().Be(expected);
        data.StartTimeUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void PausedElapsed_DerivedFromTicks()
    {
        var span = TimeSpan.FromMinutes(42);
        var data = new TimerStateData { PausedElapsedTicks = span.Ticks };

        data.PausedElapsed.Should().Be(span);
    }

    [Fact]
    public void DefaultConstructor_SetsZeroDefaults()
    {
        var data = new TimerStateData();

        data.SessionId.Should().Be(Guid.Empty);
        data.BookId.Should().Be(Guid.Empty);
        data.StartTimeTicks.Should().Be(0);
        data.IsRunning.Should().BeFalse();
        data.PausedElapsedTicks.Should().Be(0);
        data.PausedElapsed.Should().Be(TimeSpan.Zero);
    }
}


public class BookMetadataDefaultsTests
{
    [Fact]
    public void DefaultConstructor_InitializesCategoriesAsEmptyList()
    {
        var meta = new BookMetadata();

        meta.Title.Should().BeEmpty();
        meta.Author.Should().BeEmpty();
        meta.ISBN.Should().BeEmpty();
        meta.PageCount.Should().BeNull();
        meta.Categories.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Setters_PersistValues()
    {
        var meta = new BookMetadata
        {
            Title = "T",
            Author = "A",
            ISBN = "123",
            PageCount = 100,
            Publisher = "Pub",
            PublicationYear = 2020,
            CoverImageUrl = "https://cover.png",
            Description = "Desc",
            Language = "de",
            Categories = new List<string> { "Fantasy" }
        };

        meta.Title.Should().Be("T");
        meta.Categories.Should().ContainSingle().Which.Should().Be("Fantasy");
    }
}
