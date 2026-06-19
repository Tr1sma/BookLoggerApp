using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Services;

/// <summary>
/// CODE_REVIEW SEC-17: the Tropes (Plus) write path was gated only by the BookEdit.razor
/// LockedFeatureButton, never in GenreService. AddTropeToBookAsync now requires
/// <see cref="FeatureKey.Tropes"/>; RemoveTropeFromBookAsync stays open for downgrade cleanup.
/// </summary>
public class GenreServiceEntitlementTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;

    public GenreServiceEntitlementTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _cache = new ServiceCollection().AddMemoryCache().BuildServiceProvider().GetRequiredService<IMemoryCache>();
    }

    public void Dispose() => _context.Dispose();

    private GenreService CreateService(SubscriptionTier tier) =>
        new(_unitOfWork, _cache, featureGuard: new FeatureGuard(new FakeEntitlementService(tier)));

    [Fact]
    public async Task AddTropeToBookAsync_FreeUser_ThrowsAndPersistsNothing()
    {
        var bookId = Guid.NewGuid();
        var tropeId = Guid.NewGuid();
        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.AddTropeToBookAsync(bookId, tropeId);

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.Tropes);
        (await _unitOfWork.BookTropes.FindAsync(bt => bt.BookId == bookId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AddTropeToBookAsync_PlusUser_Succeeds()
    {
        var bookId = Guid.NewGuid();
        var tropeId = Guid.NewGuid();
        var service = CreateService(SubscriptionTier.Plus);

        await service.AddTropeToBookAsync(bookId, tropeId);

        (await _unitOfWork.BookTropes.FindAsync(bt => bt.BookId == bookId && bt.TropeId == tropeId))
            .Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveTropeFromBookAsync_FreeUser_NotGated_AllowsCleanup()
    {
        var bookId = Guid.NewGuid();
        var tropeId = Guid.NewGuid();
        _context.Set<BookTrope>().Add(new BookTrope { BookId = bookId, TropeId = tropeId, AddedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.RemoveTropeFromBookAsync(bookId, tropeId);

        await act.Should().NotThrowAsync();
        (await _unitOfWork.BookTropes.FindAsync(bt => bt.BookId == bookId)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetTropesForBookAsync_FreeUser_ReturnsEmptyButKeepsData()
    {
        // HIGH-1003: trope tags (Plus) carried in a restored higher-tier backup must not be
        // surfaced for a non-entitled user; the BookTrope rows stay and reappear on re-upgrade.
        var bookId = Guid.NewGuid();
        var tropeId = Guid.NewGuid();
        _context.Set<Trope>().Add(new Trope { Id = tropeId, Name = "Enemies to Lovers", GenreId = Guid.NewGuid() });
        _context.Set<BookTrope>().Add(new BookTrope { BookId = bookId, TropeId = tropeId, AddedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var service = CreateService(SubscriptionTier.Free);

        (await service.GetTropesForBookAsync(bookId)).Should().BeEmpty("Tropes are a Plus feature");
        (await _unitOfWork.BookTropes.FindAsync(bt => bt.BookId == bookId)).Should().ContainSingle("the trope tag is hidden, not deleted");
    }

    [Fact]
    public async Task GetTropesForBookAsync_PlusUser_ReturnsTropes()
    {
        var bookId = Guid.NewGuid();
        var tropeId = Guid.NewGuid();
        _context.Set<Trope>().Add(new Trope { Id = tropeId, Name = "Enemies to Lovers", GenreId = Guid.NewGuid() });
        _context.Set<BookTrope>().Add(new BookTrope { BookId = bookId, TropeId = tropeId, AddedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var service = CreateService(SubscriptionTier.Plus);

        (await service.GetTropesForBookAsync(bookId)).Should().ContainSingle()
            .Which.Name.Should().Be("Enemies to Lovers");
    }
}
