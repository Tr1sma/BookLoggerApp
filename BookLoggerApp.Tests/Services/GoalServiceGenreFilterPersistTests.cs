using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// Z.187: a genre-filtered goal rebuilds its GoalGenres navigation with synthetic instances
/// (sharing Genre references) purely for UI display while computing progress. The completion
/// persist must only flip IsCompleted/CompletedAt and must NOT write that projection graph back.
/// Runs on real SQLite — the EF InMemory provider doesn't enforce the PK/tracking semantics this
/// exercises, so the bug class can't be reproduced there.
/// </summary>
public sealed class GoalServiceGenreFilterPersistTests : IDisposable
{
    private readonly SqliteTestContext _sqlite = new();

    public void Dispose() => _sqlite.Dispose();

    [Fact]
    public async Task RecalculateGoalProgressAsync_GenreFilteredGoalAutoCompletes_PersistsWithoutCorruptingGenreGraph()
    {
        var genreId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        // EnsureCreated seeds the standard genres (Name is UNIQUE), so use a collision-proof name.
        string genreName = $"Z187_{Guid.NewGuid():N}";

        await using (var seed = _sqlite.CreateContext())
        {
            seed.Set<Genre>().Add(new Genre { Id = genreId, Name = genreName });
            seed.Set<Book>().Add(new Book
            {
                Id = bookId,
                Title = "The Filtered Book",
                Author = "Author",
                Status = ReadingStatus.Completed,
                DateAdded = DateTime.UtcNow.AddDays(-20),
                DateCompleted = DateTime.UtcNow.AddDays(-1)
            });
            seed.Set<BookGenre>().Add(new BookGenre { BookId = bookId, GenreId = genreId });
            seed.Set<ReadingGoal>().Add(new ReadingGoal
            {
                Id = goalId,
                Title = "Read 1 Fantasy book",
                Type = GoalType.Books,
                Target = 1,
                Current = 0,
                IsCompleted = false,
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(10)
            });
            seed.Set<GoalGenre>().Add(new GoalGenre { ReadingGoalId = goalId, GenreId = genreId });
            await seed.SaveChangesAsync();
        }

        await using (var ctx = _sqlite.CreateContext())
        {
            var uow = new UnitOfWork(ctx);
            var service = new GoalService(uow);

            bool newlyCompleted = await service.RecalculateGoalProgressAsync();

            newlyCompleted.Should().BeTrue("the genre-filtered goal reached its target");
        }

        await using (var verify = _sqlite.CreateContext())
        {
            var goal = await verify.Set<ReadingGoal>().SingleAsync(g => g.Id == goalId);
            goal.IsCompleted.Should().BeTrue();
            goal.CompletedAt.Should().NotBeNull();

            // The synthetic GoalGenres projection must not have leaked into the DB: the goal's
            // single association row and the untouched Genre both survive verbatim.
            (await verify.Set<GoalGenre>().CountAsync(gg => gg.ReadingGoalId == goalId))
                .Should().Be(1);
            (await verify.Set<GoalGenre>().CountAsync()).Should().Be(1);
            (await verify.Set<Genre>().SingleAsync(g => g.Id == genreId)).Name.Should().Be(genreName);
            (await verify.Set<BookGenre>().CountAsync(bg => bg.BookId == bookId && bg.GenreId == genreId))
                .Should().Be(1);
        }
    }
}
