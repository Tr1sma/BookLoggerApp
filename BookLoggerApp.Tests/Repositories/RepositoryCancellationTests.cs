using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Repositories;

/// <summary>
/// CODE_REVIEW BUG-15 / CQ-02: the bespoke repository query methods used to drop the
/// CancellationToken entirely — their terminal ToListAsync/FirstOrDefaultAsync/SumAsync ran
/// with no token, so cancellation requested by a ViewModel/navigation never reached EF.
/// These tests forward an already-cancelled token and assert the EF query observes it (EF's async
/// query pipeline calls ThrowIfCancellationRequested at entry, so a pre-cancelled token throws). A
/// real SQLite provider is used for parity with the rest of the repository/concurrency test suite,
/// not because InMemory ignores a pre-cancelled token.
/// </summary>
public class RepositoryCancellationTests : IDisposable
{
    private readonly SqliteTestContext _sqlite;

    public RepositoryCancellationTests()
    {
        _sqlite = new SqliteTestContext();
    }

    public void Dispose() => _sqlite.Dispose();

    private static CancellationToken Cancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    [Fact]
    public async Task BookRepository_GetBooksByStatusAsync_HonoursCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var repo = new BookRepository(ctx);

        Func<Task> act = () => repo.GetBooksByStatusAsync(ReadingStatus.Reading, Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BookRepository_GetBookByISBNAsync_HonoursCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var repo = new BookRepository(ctx);

        Func<Task> act = () => repo.GetBookByISBNAsync("978", Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadingSessionRepository_GetSessionsInRangeAsync_HonoursCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var repo = new ReadingSessionRepository(ctx);

        Func<Task> act = () => repo.GetSessionsInRangeAsync(
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadingSessionRepository_GetTotalMinutesReadAsync_HonoursCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var repo = new ReadingSessionRepository(ctx);

        Func<Task> act = () => repo.GetTotalMinutesReadAsync(Guid.NewGuid(), Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task UserPlantRepository_GetUserPlantsAsync_HonoursCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var repo = new UserPlantRepository(ctx);

        Func<Task> act = () => repo.GetUserPlantsAsync(Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadingGoalRepository_GetActiveGoalsAsync_HonoursCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var repo = new ReadingGoalRepository(ctx);

        Func<Task> act = () => repo.GetActiveGoalsAsync(Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
