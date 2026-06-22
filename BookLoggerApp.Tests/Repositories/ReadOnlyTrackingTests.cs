using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Repositories;

/// <summary>
/// INK-10: read-only specific repository queries must not pollute the change tracker.
/// </summary>
public class ReadOnlyTrackingTests
{
    [Fact]
    public async Task GetSessionsInRangeAsync_returns_detached_entities()
    {
        var dbName = Guid.NewGuid().ToString();
        var bookId = Guid.NewGuid();
        using (var seed = TestDbContext.Create(dbName))
        {
            seed.Books.Add(new Book { Id = bookId, Title = "T", Author = "A" });
            seed.ReadingSessions.Add(new ReadingSession
            {
                BookId = bookId,
                StartedAt = DateTime.UtcNow,
                Minutes = 10
            });
            await seed.SaveChangesAsync();
        }

        using var readCtx = TestDbContext.Create(dbName);
        var repo = new ReadingSessionRepository(readCtx);

        var sessions = (await repo.GetSessionsInRangeAsync(
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1))).ToList();

        sessions.Should().HaveCount(1);
        readCtx.Entry(sessions[0]).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task GetRecentBooksAsync_returns_detached_entities()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = TestDbContext.Create(dbName))
        {
            seed.Books.Add(new Book { Title = "Recent", Author = "A" });
            await seed.SaveChangesAsync();
        }

        using var readCtx = TestDbContext.Create(dbName);
        var repo = new BookRepository(readCtx);

        var books = (await repo.GetRecentBooksAsync(5)).ToList();

        books.Should().HaveCount(1);
        readCtx.Entry(books[0]).State.Should().Be(EntityState.Detached);
    }
}
