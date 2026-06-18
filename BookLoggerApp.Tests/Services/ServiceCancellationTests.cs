using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// CODE_REVIEW INK-05: the UnitOfWork-based services accepted a CancellationToken on every
/// public method but forwarded it only to SaveChangesAsync — every read went through a
/// specific-repository method that took no token, so the token was dropped before reaching EF.
/// After BUG-15/CQ-02 added ct to the repositories, these services now thread their ct all the
/// way to the terminal EF query. A real SQLite provider is used so cancellation is observable.
/// </summary>
public class ServiceCancellationTests : IDisposable
{
    private readonly SqliteTestContext _sqlite;

    public ServiceCancellationTests()
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
    public async Task BookService_GetByStatusAsync_PropagatesCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var service = new BookService(
            new UnitOfWork(ctx),
            Substitute.For<IProgressionService>(),
            Substitute.For<IPlantService>(),
            Substitute.For<IGoalService>(),
            NullLogger<BookService>.Instance);

        Func<Task> act = () => service.GetByStatusAsync(ReadingStatus.Reading, Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BookService_SearchAsync_PropagatesCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var service = new BookService(
            new UnitOfWork(ctx),
            Substitute.For<IProgressionService>(),
            Substitute.For<IPlantService>(),
            Substitute.For<IGoalService>(),
            NullLogger<BookService>.Instance);

        Func<Task> act = () => service.SearchAsync("anything", Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StatsService_GetReadingTrendAsync_PropagatesCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var service = new StatsService(new UnitOfWork(ctx));

        Func<Task> act = () => service.GetReadingTrendAsync(
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GoalService_GetActiveGoalsAsync_PropagatesCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var service = new GoalService(new UnitOfWork(ctx));

        Func<Task> act = () => service.GetActiveGoalsAsync(Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PlantService_GetAllAsync_PropagatesCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var cache = new ServiceCollection().AddMemoryCache().BuildServiceProvider()
            .GetRequiredService<IMemoryCache>();
        var service = new PlantService(
            new UnitOfWork(ctx),
            Substitute.For<IAppSettingsProvider>(),
            Substitute.For<IDecorationService>(),
            cache,
            NullLogger<PlantService>.Instance);

        Func<Task> act = () => service.GetAllAsync(Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadingForecastService_GetUpcomingFinishesAsync_PropagatesCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var service = new ReadingForecastService(new UnitOfWork(ctx));

        Func<Task> act = () => service.GetUpcomingFinishesAsync(Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ProgressService_GetSessionsByBookAsync_PropagatesCancellation()
    {
        await using var ctx = _sqlite.CreateContext();
        var service = new ProgressService(
            new UnitOfWork(ctx),
            Substitute.For<IProgressionService>(),
            Substitute.For<IPlantService>(),
            Substitute.For<IBookService>(),
            Substitute.For<IGoalService>(),
            Substitute.For<IDecorationService>(),
            Substitute.For<IAppSettingsProvider>());

        Func<Task> act = () => service.GetSessionsByBookAsync(Guid.NewGuid(), Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AdvancedStatsService_GetReadingHeatmapAsync_PropagatesCancellation()
    {
        var service = new AdvancedStatsService(_sqlite.CreateFactory());

        Func<Task> act = () => service.GetReadingHeatmapAsync(2026, Cancelled());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
