using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure;

/// <summary>
/// CODE_REVIEW SEC-17 (load-bearing path): after the Phase 1 BUG-16 refactor, BookEdit saves
/// tropes via <see cref="BookService.SaveBookWithRelationsAsync"/>, not GenreService. The Tropes
/// (Plus) gate must therefore be enforced there too. Adding a NEW trope tag requires Plus and
/// rolls back the whole save on violation; removing tropes stays open for downgrade cleanup.
/// Uses the SQLite fixture so the transactional rollback is real (the InMemory provider no-ops it).
/// </summary>
public class BookServiceEntitlementTests
{
    private static BookService CreateService(SqliteTestContext sqlite, SubscriptionTier tier)
    {
        var uow = new UnitOfWork(sqlite.CreateContext());
        return new BookService(
            uow, new MockProgressionService(), new MockPlantService(), new MockGoalService(), null!,
            analytics: null,
            featureGuard: new FeatureGuard(new FakeEntitlementService(tier)));
    }

    [Fact]
    public async Task SaveBookWithRelations_FreeUser_AddingNewTrope_ThrowsAndRollsBackTheBook()
    {
        using var sqlite = new SqliteTestContext();
        var service = CreateService(sqlite, SubscriptionTier.Free);

        var book = new Book { Id = Guid.NewGuid(), Title = "Tagged", Author = "A", Status = ReadingStatus.Planned };

        Func<Task> act = () => service.SaveBookWithRelationsAsync(
            book, Array.Empty<Guid>(), Array.Empty<Guid>(), new[] { Guid.NewGuid() }, Array.Empty<Guid>());

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.Tropes);

        await using var verify = sqlite.CreateContext();
        (await verify.Books.AnyAsync(b => b.Id == book.Id))
            .Should().BeFalse("the Tropes guard must roll back the entire save, not persist a half-saved book");
    }

    [Fact]
    public async Task SaveBookWithRelations_PlusUser_AddingNewTrope_Persists()
    {
        using var sqlite = new SqliteTestContext();

        Guid tropeId;
        await using (var seed = sqlite.CreateContext())
        {
            var genre = new Genre { Name = "SEC17-Genre", Icon = "🏷️" };
            seed.Genres.Add(genre);
            var trope = new Trope { Name = "Enemies to Lovers", Genre = genre };
            seed.Set<Trope>().Add(trope);
            await seed.SaveChangesAsync();
            tropeId = trope.Id;
        }

        var service = CreateService(sqlite, SubscriptionTier.Plus);
        var book = new Book { Id = Guid.NewGuid(), Title = "Tagged", Author = "A", Status = ReadingStatus.Planned };

        await service.SaveBookWithRelationsAsync(
            book, Array.Empty<Guid>(), Array.Empty<Guid>(), new[] { tropeId }, Array.Empty<Guid>());

        await using var verify = sqlite.CreateContext();
        (await verify.BookTropes.AnyAsync(bt => bt.BookId == book.Id && bt.TropeId == tropeId)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveBookWithRelations_FreeUser_RemovingTrope_NotGated()
    {
        using var sqlite = new SqliteTestContext();

        Guid bookId = Guid.NewGuid();
        Guid tropeId;
        await using (var seed = sqlite.CreateContext())
        {
            var genre = new Genre { Name = "SEC17-Genre2", Icon = "🏷️" };
            seed.Genres.Add(genre);
            var trope = new Trope { Name = "Slow Burn", Genre = genre };
            seed.Set<Trope>().Add(trope);
            seed.Books.Add(new Book { Id = bookId, Title = "Old", Author = "A", Status = ReadingStatus.Planned, DateAdded = DateTime.UtcNow });
            await seed.SaveChangesAsync();
            tropeId = trope.Id;
            seed.BookTropes.Add(new BookTrope { BookId = bookId, TropeId = tropeId, AddedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        var service = CreateService(sqlite, SubscriptionTier.Free);
        var edited = new Book { Id = bookId, Title = "Old", Author = "A", Status = ReadingStatus.Planned, DateAdded = DateTime.UtcNow };

        // Save with NO tropes => the existing trope is a removal (net-new is empty) => must not be gated.
        Func<Task> act = () => service.SaveBookWithRelationsAsync(
            edited, Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>());

        await act.Should().NotThrowAsync("removing trope tags must stay open so downgraded users can clean up");

        await using var verify = sqlite.CreateContext();
        (await verify.BookTropes.AnyAsync(bt => bt.BookId == bookId)).Should().BeFalse();
    }
}
