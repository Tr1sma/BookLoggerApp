using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure;

/// <summary>
/// Covers CODE_REVIEW BUG-06: StatsTrendsViewModel/StatsAnalysesViewModel start 5-7
/// IAdvancedStatsService queries via Task.WhenAll. The service used a single shared
/// IUnitOfWork (one DbContext), and EF Core forbids concurrent operations on one context
/// ("A second operation was started on this context instance..."). The fix gives every
/// query its own DbContext from an IDbContextFactory; this test pins that contract
/// deterministically by counting context creations.
/// </summary>
public class AdvancedStatsServiceContextTests
{
    [Fact]
    public async Task AdvancedStats_OpensAFreshDbContextPerCall()
    {
        using var sqlite = new SqliteTestContext();
        var factory = new CountingFactory(sqlite);
        var service = new AdvancedStatsService(factory);

        // Three independent query paths — the kind the Stats VMs fan out concurrently.
        await service.GetWeekdayDistributionAsync();
        await service.GetTimeOfDayDistributionAsync();
        await service.GetCompletionRateAsync();

        factory.CreatedCount.Should().Be(3,
            "each query path must open its own DbContext so a Task.WhenAll fan-out never shares one context");
    }

    [Fact]
    public async Task GetYearComparison_RunsWithoutSharingTheParentContextAcrossYears()
    {
        using var sqlite = new SqliteTestContext();
        var factory = new CountingFactory(sqlite);
        var service = new AdvancedStatsService(factory);

        var (year1, year2) = await service.GetYearComparisonAsync(2024, 2025);

        year1.Year.Should().Be(2024);
        year2.Year.Should().Be(2025);
        factory.CreatedCount.Should().BeGreaterThan(0, "the comparison must run through the factory, not a shared injected context");
    }

    private sealed class CountingFactory : IDbContextFactory<AppDbContext>
    {
        private readonly SqliteTestContext _owner;
        private int _count;
        public CountingFactory(SqliteTestContext owner) => _owner = owner;
        public int CreatedCount => _count;

        public AppDbContext CreateDbContext()
        {
            Interlocked.Increment(ref _count);
            return _owner.CreateContext();
        }
    }
}
