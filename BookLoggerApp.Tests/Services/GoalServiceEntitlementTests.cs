using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// CODE_REVIEW SEC-03 / SEC-07 / SEC-10: the Premium genre/excluded-book goal filters were
/// only gated by the Goals.razor LockedFeatureButton, never in the service, so the exclude
/// modal let any tier attach genre filters / book exclusions via the incremental mutators.
/// The service now requires <see cref="FeatureKey.ReadingGoalsWithGenreTropeFilter"/> on the
/// write path; the remove/include cleanup paths stay open so downgraded users can clear filters.
/// </summary>
public class GoalServiceEntitlementTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public GoalServiceEntitlementTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
    }

    public void Dispose() => _context.Dispose();

    private GoalService CreateService(SubscriptionTier tier) =>
        new(_unitOfWork, analytics: null, featureGuard: new FeatureGuard(new FakeEntitlementService(tier)));

    private async Task<ReadingGoal> SeedGoalAsync()
    {
        var goal = new ReadingGoal
        {
            Id = Guid.NewGuid(),
            Title = "Plain Goal",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var result = await _unitOfWork.ReadingGoals.AddAsync(goal);
        await _unitOfWork.SaveChangesAsync();
        return result;
    }

    [Fact]
    public async Task AddGenreToGoalAsync_PlusUser_ThrowsAndPersistsNothing()
    {
        var goal = await SeedGoalAsync();
        var service = CreateService(SubscriptionTier.Plus);

        Func<Task> act = () => service.AddGenreToGoalAsync(goal.Id, Guid.NewGuid());

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.ReadingGoalsWithGenreTropeFilter);
        (await service.GetGoalGenresAsync(goal.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ExcludeBookFromGoalAsync_PlusUser_ThrowsAndPersistsNothing()
    {
        var goal = await SeedGoalAsync();
        var service = CreateService(SubscriptionTier.Plus);

        Func<Task> act = () => service.ExcludeBookFromGoalAsync(goal.Id, Guid.NewGuid());

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.ReadingGoalsWithGenreTropeFilter);
        (await service.GetExcludedBooksAsync(goal.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ExcludeBookFromGoalAsync_PremiumUser_Succeeds()
    {
        var goal = await SeedGoalAsync();
        var bookId = Guid.NewGuid();
        var service = CreateService(SubscriptionTier.Premium);

        await service.ExcludeBookFromGoalAsync(goal.Id, bookId);

        (await service.GetExcludedBooksAsync(goal.Id)).Should().ContainSingle()
            .Which.BookId.Should().Be(bookId);
    }

    [Fact]
    public async Task AddGenreToGoalAsync_PremiumUser_Succeeds()
    {
        var goal = await SeedGoalAsync();
        var genreId = Guid.NewGuid();
        var service = CreateService(SubscriptionTier.Premium);

        await service.AddGenreToGoalAsync(goal.Id, genreId);

        (await service.GetGoalGenresAsync(goal.Id)).Should().ContainSingle()
            .Which.GenreId.Should().Be(genreId);
    }

    [Fact]
    public async Task RemoveGenreFromGoalAsync_FreeUser_NotGated_AllowsCleanup()
    {
        // Seed an existing filter directly (as if granted while the user still had Premium).
        var goal = await SeedGoalAsync();
        var genreId = Guid.NewGuid();
        _context.Set<GoalGenre>().Add(new GoalGenre { ReadingGoalId = goal.Id, GenreId = genreId });
        await _context.SaveChangesAsync();

        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.RemoveGenreFromGoalAsync(goal.Id, genreId);

        await act.Should().NotThrowAsync("downgraded users must be able to remove a previously-set filter");
        (await service.GetGoalGenresAsync(goal.Id)).Should().BeEmpty();
    }
}
