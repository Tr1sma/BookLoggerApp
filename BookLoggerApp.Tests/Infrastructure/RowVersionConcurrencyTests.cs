using BookLoggerApp.Core.Models;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure;

/// <summary>
/// Proves that optimistic concurrency via RowVersion actually works on SQLite.
/// Covers CODE_REVIEW findings BUG-01 / BUG-10 / SEC-09 / INK-07: the [Timestamp]
/// RowVersion tokens were never bumped on SQLite, so DbUpdateConcurrencyException
/// could never fire and the concurrency-catch blocks were dead code.
///
/// These tests require a real SQLite engine (see <see cref="SqliteTestContext"/>);
/// the EF InMemory provider cannot reproduce concurrency-token behaviour.
/// </summary>
public class RowVersionConcurrencyTests
{
    [Fact]
    public async Task SaveChanges_StampsRowVersion_AndChangesItOnEverySave()
    {
        using var sqlite = new SqliteTestContext();

        await using var context = sqlite.CreateContext();
        var settings = await context.AppSettings.FirstAsync();

        settings.Coins += 10;
        await context.SaveChangesAsync();
        byte[]? afterFirstSave = settings.RowVersion;

        settings.Coins += 10;
        await context.SaveChangesAsync();
        byte[]? afterSecondSave = settings.RowVersion;

        afterFirstSave.Should().NotBeNull("a RowVersion must be stamped on save so concurrency checks work");
        afterSecondSave.Should().NotBeNull();
        afterSecondSave.Should().NotEqual(afterFirstSave, "the token must change on every save or stale writes can't be detected");
    }

    [Fact]
    public async Task StaleUpdate_AfterConcurrentWrite_Throws_DbUpdateConcurrencyException()
    {
        using var sqlite = new SqliteTestContext();

        // Two independent contexts load the same AppSettings row.
        await using var ctx1 = sqlite.CreateContext();
        await using var ctx2 = sqlite.CreateContext();

        var s1 = await ctx1.AppSettings.FirstAsync();
        var s2 = await ctx2.AppSettings.FirstAsync();

        // First writer wins and bumps the RowVersion.
        s1.Coins += 10;
        await ctx1.SaveChangesAsync();

        // Second writer holds a now-stale RowVersion → must be detected as a conflict.
        s2.Coins += 5;
        Func<Task> staleSave = async () => await ctx2.SaveChangesAsync();

        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
