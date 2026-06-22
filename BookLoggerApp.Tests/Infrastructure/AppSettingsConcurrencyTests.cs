using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure;

/// <summary>
/// Covers CODE_REVIEW findings BUG-08 / INK-03 / SEC-12: the singleton
/// <see cref="AppSettingsProvider"/> cache is mutated through several write paths, and the
/// entitlement mirror sync used a full-entity update that could clobber concurrent XP/coin
/// writes from a stale cached instance. These need a real SQLite engine (working RowVersion
/// concurrency token) — see <see cref="SqliteTestContext"/>.
/// </summary>
public class AppSettingsConcurrencyTests
{
    [Fact]
    public async Task UpdateEntitlementMirror_DoesNotClobberConcurrentCoinOrXpWrites()
    {
        using var sqlite = new SqliteTestContext();
        var provider = new AppSettingsProvider(sqlite.CreateFactory());

        // Prime the singleton cache with the default settings instance.
        AppSettings cached = await provider.GetSettingsAsync();
        int seededCoins = cached.Coins;

        // A concurrent writer awards coins + XP directly to the DB row, so the provider's
        // cached instance is now stale relative to the persisted state.
        await using (var ctx = sqlite.CreateContext())
        {
            var dbSettings = await ctx.AppSettings.FirstAsync();
            dbSettings.Coins += 500;
            dbSettings.TotalXp += 1234;
            await ctx.SaveChangesAsync();
        }

        // Mirror the entitlement tier. A full-entity update from the stale cache would reset
        // Coins/TotalXp to the cached values; a narrow update must touch only the two
        // mirror columns and leave the concurrent progression award intact.
        await provider.UpdateEntitlementMirrorAsync(SubscriptionTier.Premium, null);

        await using var verify = sqlite.CreateContext();
        var saved = await verify.AppSettings.FirstAsync();

        saved.CurrentTier.Should().Be(SubscriptionTier.Premium, "the mirror must still update the tier");
        saved.Coins.Should().Be(seededCoins + 500, "the entitlement mirror must not clobber concurrent coin awards");
        saved.TotalXp.Should().Be(1234, "the entitlement mirror must not clobber concurrent XP awards");
    }

    [Fact]
    public async Task AddCoins_UnderConcurrentLoad_NeverLosesAnUpdate()
    {
        using var sqlite = new SqliteTestContext();
        var provider = new AppSettingsProvider(sqlite.CreateFactory());

        int seededCoins = (await provider.GetSettingsAsync()).Coins;

        const int writers = 20;
        var tasks = Enumerable.Range(0, writers)
            .Select(_ => Task.Run(() => provider.AddCoinsAsync(1)))
            .ToArray();
        await Task.WhenAll(tasks);

        await using var verify = sqlite.CreateContext();
        var saved = await verify.AppSettings.FirstAsync();
        saved.Coins.Should().Be(seededCoins + writers,
            "serialised read-modify-write must apply every concurrent coin award exactly once");
    }
}
