using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class BlindDateServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly BlindDateService _service;

    public BlindDateServiceTests()
    {
        _factory = new TestDbContextFactory(Guid.NewGuid().ToString());
        _service = new BlindDateService(_factory);
    }

    public void Dispose()
    {
        using var ctx = _factory.CreateDbContext();
        ctx.Database.EnsureDeleted();
    }

    private async Task SeedBookAsync(string title, ReadingStatus status, string[]? tropeNames = null, string[]? genreNames = null)
    {
        await using var ctx = _factory.CreateDbContext();
        var book = new Book { Title = title, Author = "Author", Status = status };
        ctx.Books.Add(book);

        if (tropeNames is not null)
        {
            foreach (var name in tropeNames)
            {
                var trope = new Trope { Id = Guid.NewGuid(), Name = name, GenreId = Guid.NewGuid() };
                ctx.Tropes.Add(trope);
                ctx.BookTropes.Add(new BookTrope { BookId = book.Id, TropeId = trope.Id });
            }
        }

        if (genreNames is not null)
        {
            foreach (var name in genreNames)
            {
                var genre = new Genre { Id = Guid.NewGuid(), Name = name };
                ctx.Genres.Add(genre);
                ctx.BookGenres.Add(new BookGenre { BookId = book.Id, GenreId = genre.Id });
            }
        }

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCandidatesAsync_ReturnsPlannedAndWishlist_ExcludesRead()
    {
        await SeedBookAsync("Planned", ReadingStatus.Planned);
        await SeedBookAsync("Wishlist", ReadingStatus.Wishlist);
        await SeedBookAsync("Reading", ReadingStatus.Reading);
        await SeedBookAsync("Completed", ReadingStatus.Completed);
        await SeedBookAsync("Abandoned", ReadingStatus.Abandoned);

        var result = await _service.GetCandidatesAsync();

        result.Select(b => b.Title).Should().BeEquivalentTo("Planned", "Wishlist");
    }

    [Fact]
    public async Task GetCandidatesAsync_EagerLoadsTropes()
    {
        await SeedBookAsync("Vibes", ReadingStatus.Planned, tropeNames: new[] { "Slow Burn", "Enemies to Lovers" });

        var result = await _service.GetCandidatesAsync();

        result.Should().HaveCount(1);
        result[0].BookTropes.Select(bt => bt.Trope.Name)
            .Should().BeEquivalentTo("Slow Burn", "Enemies to Lovers");
    }

    [Fact]
    public async Task GetCandidatesAsync_EagerLoadsGenres()
    {
        await SeedBookAsync("Genred", ReadingStatus.Wishlist, genreNames: new[] { "Fantasy" });

        var result = await _service.GetCandidatesAsync();

        result.Should().HaveCount(1);
        result[0].BookGenres.Select(bg => bg.Genre.Name).Should().ContainSingle().Which.Should().Be("Fantasy");
    }

    [Fact]
    public async Task GetCandidatesAsync_ReturnsEmpty_WhenNoUnreadBooks()
    {
        await SeedBookAsync("Reading", ReadingStatus.Reading);
        await SeedBookAsync("Completed", ReadingStatus.Completed);

        var result = await _service.GetCandidatesAsync();

        result.Should().BeEmpty();
    }
}
