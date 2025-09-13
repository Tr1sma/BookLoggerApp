using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services;
using FluentAssertions;
using SQLite;
using Xunit;
using static BookLoggerApp.Tests.TestHelpers.TestDb;

namespace BookLoggerApp.Tests.Services;

public class ProgressServiceTests
{
    [Fact]
    public async Task AddSessions_Sums_Minutes()
    {
        var path = NewPath();
        try
        {
            // Create connection & service
            var conn = new SQLiteAsyncConnection(path);
            var progress = await SqliteProgressService.CreateAsync(path);

            var bookId = Guid.NewGuid();

            await progress.AddSessionAsync(new ReadingSession { BookId = bookId, Minutes = 20 });
            await progress.AddSessionAsync(new ReadingSession { BookId = bookId, Minutes = 15 });

            var total = await progress.GetTotalMinutesAsync(bookId);
            total.Should().Be(35);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task GetSessionsByBook_Returns_Descending_By_Date()
    {
        var path = NewPath();
        try
        {
            var progress = await SqliteProgressService.CreateAsync(path);
            var bookId = Guid.NewGuid();

            await progress.AddSessionAsync(new ReadingSession { BookId = bookId, Minutes = 10, StartedAt = DateTime.UtcNow.AddHours(-2) });
            await progress.AddSessionAsync(new ReadingSession { BookId = bookId, Minutes = 10, StartedAt = DateTime.UtcNow.AddHours(-1) });
            await progress.AddSessionAsync(new ReadingSession { BookId = bookId, Minutes = 10, StartedAt = DateTime.UtcNow });

            var list = await progress.GetSessionsByBookAsync(bookId);
            list.Select(s => s.StartedAt).Should().BeInDescendingOrder();
        }
        finally { TryDelete(path); }
    }
}
