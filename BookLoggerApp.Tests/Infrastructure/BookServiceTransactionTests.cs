using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure;

/// <summary>
/// Covers CODE_REVIEW BUG-16: BookEditViewModel.SaveAsync orchestrated ~10 independent
/// service calls with no enclosing transaction, so a mid-save failure left a half-saved
/// book. SaveBookWithRelationsAsync now wraps the book + genre/shelf/trope/wishlist writes
/// in one transaction. Requires a real SQLite engine (FK enforcement + real transactions) —
/// the EF InMemory provider can model neither.
/// </summary>
public class BookServiceTransactionTests
{
    private static BookService CreateService(SqliteTestContext sqlite)
    {
        var uow = new UnitOfWork(sqlite.CreateContext());
        return new BookService(uow, new MockProgressionService(), new MockPlantService(), new MockGoalService(), null!);
    }

    [Fact]
    public async Task SaveBookWithRelations_WhenARelationViolatesFk_RollsBackTheBook()
    {
        using var sqlite = new SqliteTestContext();
        var service = CreateService(sqlite);

        var book = new Book { Id = Guid.NewGuid(), Title = "Half", Author = "A", Status = ReadingStatus.Planned };
        var bogusGenreId = Guid.NewGuid(); // no such Genre row → FK violation on save

        Func<Task> act = () => service.SaveBookWithRelationsAsync(
            book, new[] { bogusGenreId }, Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>());

        await act.Should().ThrowAsync<DbUpdateException>();

        await using var verify = sqlite.CreateContext();
        (await verify.Books.AnyAsync(b => b.Id == book.Id))
            .Should().BeFalse("a failed relation sync must roll back the book insert (BUG-16)");
    }

    [Fact]
    public async Task SaveBookWithRelations_NewBook_PersistsBookGenresAndShelfAtPositionZero()
    {
        using var sqlite = new SqliteTestContext();
        var service = CreateService(sqlite);

        Guid genreId, shelfId;
        await using (var seed = sqlite.CreateContext())
        {
            var genre = new Genre { Name = "BUG16-Genre", Icon = "🧙" };
            var shelf = new Shelf { Name = "BUG16-Shelf" };
            seed.Genres.Add(genre);
            seed.Shelves.Add(shelf);
            await seed.SaveChangesAsync();
            genreId = genre.Id;
            shelfId = shelf.Id;
        }

        var book = new Book { Id = Guid.NewGuid(), Title = "Atomic", Author = "A", Status = ReadingStatus.Planned };

        var result = await service.SaveBookWithRelationsAsync(
            book, new[] { genreId }, new[] { shelfId }, Array.Empty<Guid>(), new[] { shelfId });

        result.ShowCompletionCelebration.Should().BeFalse();

        await using var verify = sqlite.CreateContext();
        (await verify.Books.AnyAsync(b => b.Id == book.Id)).Should().BeTrue();
        (await verify.BookGenres.AnyAsync(bg => bg.BookId == book.Id && bg.GenreId == genreId)).Should().BeTrue();
        var shelfRow = await verify.BookShelves.FirstAsync(bs => bs.BookId == book.Id && bs.ShelfId == shelfId);
        shelfRow.Position.Should().Be(0);
    }

    [Fact]
    public async Task SaveBookWithRelations_ExistingBookCompleted_SignalsCelebrationAndStampsDate()
    {
        using var sqlite = new SqliteTestContext();
        var service = CreateService(sqlite);

        var bookId = Guid.NewGuid();
        await using (var seed = sqlite.CreateContext())
        {
            seed.Books.Add(new Book
            {
                Id = bookId, Title = "WIP", Author = "A",
                Status = ReadingStatus.Reading, DateAdded = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var edited = new Book
        {
            Id = bookId, Title = "WIP", Author = "A",
            Status = ReadingStatus.Completed, DateAdded = DateTime.UtcNow
        };

        var result = await service.SaveBookWithRelationsAsync(
            edited, Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>());

        result.ShowCompletionCelebration.Should().BeTrue();
        result.CompletedFromExisting.Should().BeTrue();

        await using var verify = sqlite.CreateContext();
        var saved = await verify.Books.FirstAsync(b => b.Id == bookId);
        saved.Status.Should().Be(ReadingStatus.Completed);
        saved.DateCompleted.Should().NotBeNull("CompleteBookAsync runs after commit and stamps DateCompleted");
    }
}
