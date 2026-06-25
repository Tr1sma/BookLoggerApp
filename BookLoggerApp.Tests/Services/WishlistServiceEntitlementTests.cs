using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// Verifies WishlistService requires <see cref="FeatureKey.Wishlist"/> on create/edit paths,
/// while read and MoveToLibrary stay open so downgraded users can migrate existing data.
/// </summary>
public class WishlistServiceEntitlementTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly ILookupService _lookupService;
    private readonly string _dbName;

    public WishlistServiceEntitlementTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _factory = new TestDbContextFactory(_dbName);
        _lookupService = Substitute.For<ILookupService>();
    }

    public void Dispose()
    {
        using var ctx = _factory.CreateDbContext();
        ctx.Database.EnsureDeleted();
    }

    private WishlistService CreateService(SubscriptionTier tier) =>
        new(_factory, _lookupService, featureGuard: new FeatureGuard(new FakeEntitlementService(tier)));

    [Fact]
    public async Task AddToWishlistAsync_FreeUser_ThrowsAndPersistsNothing()
    {
        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.AddToWishlistAsync(new Book { Title = "T", Author = "A" });

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.Wishlist);

        await using var verify = _factory.CreateDbContext();
        (await verify.Books.AnyAsync(b => b.Status == ReadingStatus.Wishlist))
            .Should().BeFalse("the guard must throw before any wishlist book is persisted");
    }

    [Fact]
    public async Task AddToWishlistAsync_PlusUser_Succeeds()
    {
        var service = CreateService(SubscriptionTier.Plus);

        var book = await service.AddToWishlistAsync(new Book { Title = "T", Author = "A" });

        book.Status.Should().Be(ReadingStatus.Wishlist);
    }

    [Fact]
    public async Task AddToWishlistByIsbnAsync_FreeUser_ThrowsBeforeLookup()
    {
        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.AddToWishlistByIsbnAsync("9780000000001");

        await act.Should().ThrowAsync<EntitlementRequiredException>();
        await _lookupService.DidNotReceive().LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWishlistInfoAsync_FreeUser_Throws()
    {
        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.UpdateWishlistInfoAsync(Guid.NewGuid(), WishlistPriority.High, null, null);

        await act.Should().ThrowAsync<EntitlementRequiredException>();
    }

    [Fact]
    public async Task ClearWishlistInfoAsync_FreeUser_Throws()
    {
        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.ClearWishlistInfoAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<EntitlementRequiredException>();
    }

    [Fact]
    public async Task MoveToLibraryAsync_FreeUser_NotGated()
    {
        // Seed a wishlist book directly (granted while the user still had Plus).
        Guid bookId;
        await using (var ctx = _factory.CreateDbContext())
        {
            var book = new Book { Title = "Old Wish", Author = "A", Status = ReadingStatus.Wishlist, DateAdded = DateTime.UtcNow };
            ctx.Books.Add(book);
            await ctx.SaveChangesAsync();
            bookId = book.Id;
        }

        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.MoveToLibraryAsync(bookId);

        await act.Should().NotThrowAsync("downgraded users must be able to migrate existing wishlist books to their library");

        await using var verify = _factory.CreateDbContext();
        (await verify.Books.FirstAsync(b => b.Id == bookId)).Status.Should().Be(ReadingStatus.Planned);
    }

    [Fact]
    public async Task GetWishlistBooksAsync_FreeUser_ReturnsEmptyButKeepsData()
    {
        // A restored Plus account leaves wishlist books in the DB. A Free user must not see them
        // (read gated), but the data is preserved and reappears on re-upgrade.
        Guid bookId;
        await using (var ctx = _factory.CreateDbContext())
        {
            var book = new Book { Title = "Restored Wish", Author = "A", Status = ReadingStatus.Wishlist, DateAdded = DateTime.UtcNow };
            ctx.Books.Add(book);
            await ctx.SaveChangesAsync();
            bookId = book.Id;
        }

        var service = CreateService(SubscriptionTier.Free);

        (await service.GetWishlistBooksAsync()).Should().BeEmpty("a non-entitled user must not see restored wishlist books");
        (await service.GetWishlistCountAsync()).Should().Be(0);
        (await service.SearchWishlistAsync("Restored")).Should().BeEmpty();

        await using var verify = _factory.CreateDbContext();
        (await verify.Books.AnyAsync(b => b.Id == bookId)).Should().BeTrue("the wishlist book is hidden, not deleted");
    }

    [Fact]
    public async Task GetWishlistBooksAsync_PlusUser_ReturnsBooks()
    {
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Books.Add(new Book { Title = "Restored Wish", Author = "A", Status = ReadingStatus.Wishlist, DateAdded = DateTime.UtcNow });
            await ctx.SaveChangesAsync();
        }

        var service = CreateService(SubscriptionTier.Plus);

        (await service.GetWishlistBooksAsync()).Should().ContainSingle();
        (await service.GetWishlistCountAsync()).Should().Be(1);
        (await service.SearchWishlistAsync("Restored")).Should().ContainSingle();
    }
}
