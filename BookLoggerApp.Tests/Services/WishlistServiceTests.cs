using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class WishlistServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly ILookupService _lookupService;
    private readonly WishlistService _service;
    private readonly string _dbName;

    public WishlistServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _factory = new TestDbContextFactory(_dbName);
        _lookupService = Substitute.For<ILookupService>();
        _service = new WishlistService(_factory, _lookupService);
    }

    public void Dispose()
    {
        using var ctx = _factory.CreateDbContext();
        ctx.Database.EnsureDeleted();
    }

    private async Task<Book> SeedWishlistBookAsync(string title, DateTime? dateAdded = null, WishlistPriority priority = WishlistPriority.Medium)
    {
        await using var ctx = _factory.CreateDbContext();
        var book = new Book
        {
            Title = title,
            Author = "Author",
            Status = ReadingStatus.Wishlist,
            DateAdded = DateTime.UtcNow
        };
        ctx.Books.Add(book);
        ctx.WishlistInfos.Add(new WishlistInfo
        {
            BookId = book.Id,
            Priority = priority,
            DateAddedToWishlist = dateAdded ?? DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        return book;
    }

    [Fact]
    public async Task GetWishlistBooksAsync_FiltersByStatus()
    {
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Books.Add(new Book { Title = "W", Author = "a", Status = ReadingStatus.Wishlist });
            ctx.Books.Add(new Book { Title = "P", Author = "a", Status = ReadingStatus.Planned });
            ctx.Books.Add(new Book { Title = "R", Author = "a", Status = ReadingStatus.Reading });
            await ctx.SaveChangesAsync();
        }

        var result = await _service.GetWishlistBooksAsync();

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("W");
    }

    [Fact]
    public async Task GetWishlistBooksAsync_SortsByDateAddedDescending()
    {
        var older = DateTime.UtcNow.AddDays(-5);
        var newer = DateTime.UtcNow.AddDays(-1);
        await SeedWishlistBookAsync("Older", older);
        await SeedWishlistBookAsync("Newer", newer);

        var result = await _service.GetWishlistBooksAsync();

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Newer");
        result[1].Title.Should().Be("Older");
    }

    [Fact]
    public async Task AddToWishlistAsync_WithoutInfo_CreatesDefaultMediumPriority()
    {
        var book = new Book { Title = "New Wish", Author = "a" };

        var result = await _service.AddToWishlistAsync(book);

        result.Status.Should().Be(ReadingStatus.Wishlist);
        result.WishlistInfo.Should().NotBeNull();
        result.WishlistInfo!.Priority.Should().Be(WishlistPriority.Medium);
    }

    [Fact]
    public async Task AddToWishlistAsync_WithProvidedInfo_UsesIt()
    {
        var book = new Book { Title = "W", Author = "a" };
        var info = new WishlistInfo { Priority = WishlistPriority.High, RecommendedBy = "Friend" };

        var result = await _service.AddToWishlistAsync(book, info);

        result.WishlistInfo!.Priority.Should().Be(WishlistPriority.High);
        result.WishlistInfo.RecommendedBy.Should().Be("Friend");
    }

    [Fact]
    public async Task AddToWishlistByIsbnAsync_LookupReturnsMetadata_CreatesBook()
    {
        _lookupService.LookupByISBNAsync("9781234567890", Arg.Any<CancellationToken>())
            .Returns(new BookMetadata
            {
                Title = "Found Book",
                Author = "Looked Up",
                ISBN = "9781234567890",
                PageCount = 200
            });

        var result = await _service.AddToWishlistByIsbnAsync("9781234567890");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Found Book");
        result.Status.Should().Be(ReadingStatus.Wishlist);
    }

    [Fact]
    public async Task AddToWishlistByIsbnAsync_LookupReturnsNull_ReturnsNull()
    {
        _lookupService.LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((BookMetadata?)null);

        var result = await _service.AddToWishlistByIsbnAsync("0000000000");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateWishlistInfoAsync_UpdatesExistingInfo()
    {
        var book = await SeedWishlistBookAsync("B");

        await _service.UpdateWishlistInfoAsync(book.Id, WishlistPriority.High, "Jane", "Must read");

        await using var ctx = _factory.CreateDbContext();
        var info = await ctx.WishlistInfos.FindAsync(book.Id);
        info!.Priority.Should().Be(WishlistPriority.High);
        info.RecommendedBy.Should().Be("Jane");
        info.WishlistNotes.Should().Be("Must read");
    }

    [Fact]
    public async Task UpdateWishlistInfoAsync_NonExistingInfo_IsNoOp()
    {
        Func<Task> act = async () => await _service.UpdateWishlistInfoAsync(Guid.NewGuid(), WishlistPriority.Low, null, null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MoveToLibraryAsync_SetsStatusAndRemovesInfo()
    {
        var book = await SeedWishlistBookAsync("Book");

        await _service.MoveToLibraryAsync(book.Id);

        await using var ctx = _factory.CreateDbContext();
        var reloaded = await ctx.Books.FindAsync(book.Id);
        reloaded!.Status.Should().Be(ReadingStatus.Planned);
        reloaded.CurrentPage.Should().Be(0);

        var info = await ctx.WishlistInfos.FindAsync(book.Id);
        info.Should().BeNull();
    }

    [Fact]
    public async Task MoveToLibraryAsync_NonExistingBook_IsNoOp()
    {
        Func<Task> act = async () => await _service.MoveToLibraryAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveFromWishlistAsync_RemovesBook()
    {
        var book = await SeedWishlistBookAsync("Rm");

        await _service.RemoveFromWishlistAsync(book.Id);

        await using var ctx = _factory.CreateDbContext();
        var reloaded = await ctx.Books.FindAsync(book.Id);
        reloaded.Should().BeNull();
    }

    [Fact]
    public async Task RemoveFromWishlistAsync_NonExistingBook_IsNoOp()
    {
        Func<Task> act = async () => await _service.RemoveFromWishlistAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetWishlistCountAsync_CountsOnlyWishlistStatus()
    {
        await SeedWishlistBookAsync("W1");
        await SeedWishlistBookAsync("W2");
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Books.Add(new Book { Title = "P", Author = "a", Status = ReadingStatus.Planned });
            await ctx.SaveChangesAsync();
        }

        var count = await _service.GetWishlistCountAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task ClearWishlistInfoAsync_RemovesInfoNotBook()
    {
        var book = await SeedWishlistBookAsync("B");

        await _service.ClearWishlistInfoAsync(book.Id);

        await using var ctx = _factory.CreateDbContext();
        (await ctx.WishlistInfos.FindAsync(book.Id)).Should().BeNull();
        (await ctx.Books.FindAsync(book.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ClearWishlistInfoAsync_NonExistingInfo_IsNoOp()
    {
        Func<Task> act = async () => await _service.ClearWishlistInfoAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SearchWishlistAsync_MatchesTitleCaseInsensitive()
    {
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Books.Add(new Book { Title = "Dune", Author = "Frank", Status = ReadingStatus.Wishlist });
            ctx.Books.Add(new Book { Title = "Foundation", Author = "Isaac", Status = ReadingStatus.Wishlist });
            await ctx.SaveChangesAsync();
        }

        var result = await _service.SearchWishlistAsync("DUNE");

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Dune");
    }

    [Fact]
    public async Task SearchWishlistAsync_MatchesAuthorCaseInsensitive()
    {
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Books.Add(new Book { Title = "X", Author = "Frank Herbert", Status = ReadingStatus.Wishlist });
            ctx.Books.Add(new Book { Title = "Y", Author = "Isaac Asimov", Status = ReadingStatus.Wishlist });
            await ctx.SaveChangesAsync();
        }

        var result = await _service.SearchWishlistAsync("herbert");

        result.Should().HaveCount(1);
        result[0].Author.Should().Be("Frank Herbert");
    }

    [Fact]
    public async Task SearchWishlistAsync_MatchesIsbn()
    {
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Books.Add(new Book { Title = "X", Author = "a", ISBN = "9781111111111", Status = ReadingStatus.Wishlist });
            ctx.Books.Add(new Book { Title = "Y", Author = "a", ISBN = "9782222222222", Status = ReadingStatus.Wishlist });
            await ctx.SaveChangesAsync();
        }

        var result = await _service.SearchWishlistAsync("9781111");

        result.Should().HaveCount(1);
        result[0].ISBN.Should().Be("9781111111111");
    }

    [Fact]
    public async Task SearchWishlistAsync_ExcludesNonWishlistBooks()
    {
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Books.Add(new Book { Title = "ReadingMatch", Author = "a", Status = ReadingStatus.Reading });
            ctx.Books.Add(new Book { Title = "WishMatch", Author = "a", Status = ReadingStatus.Wishlist });
            await ctx.SaveChangesAsync();
        }

        var result = await _service.SearchWishlistAsync("match");

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(ReadingStatus.Wishlist);
    }
}
