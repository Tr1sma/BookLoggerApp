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
/// SEC-03 / SEC-07 / SEC-10: Premium genre/excluded-book goal filters were only gated in the UI,
/// so any tier could attach them via the incremental mutators. The service now requires
/// <see cref="FeatureKey.ReadingGoalsWithGenreTropeFilter"/> on the write path; remove/include
/// cleanup stays open so downgraded users can clear filters.
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

    private async Task<ReadingGoal> SeedBooksGoalWithExcludedBookAsync()
    {
        var goal = new ReadingGoal
        {
            Id = Guid.NewGuid(),
            Title = "Filtered Goal",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var keep = new Book { Id = Guid.NewGuid(), Title = "Keep", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var excluded = new Book { Id = Guid.NewGuid(), Title = "Excluded", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        _context.ReadingGoals.Add(goal);
        _context.Books.AddRange(keep, excluded);
        _context.Set<GoalExcludedBook>().Add(new GoalExcludedBook { ReadingGoalId = goal.Id, BookId = excluded.Id });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return goal;
    }

    [Fact]
    public async Task CalculateProgress_FreeUser_IgnoresExcludedBookFilter()
    {
        // HIGH-1003: a restored Premium goal carries a Premium-only book exclusion. A Free user
        // isn't entitled to it, so progress is unfiltered (both books count); the row is preserved
        // and re-applies on re-upgrade.
        await SeedBooksGoalWithExcludedBookAsync();
        var service = CreateService(SubscriptionTier.Free);

        var goals = await service.GetActiveGoalsAsync();

        goals.Should().ContainSingle().Which.Current.Should().Be(2,
            "a Free user is not entitled to excluded-book filters, so all completed books count");
    }

    [Fact]
    public async Task CalculateProgress_PremiumUser_AppliesExcludedBookFilter()
    {
        await SeedBooksGoalWithExcludedBookAsync();
        var service = CreateService(SubscriptionTier.Premium);

        var goals = await service.GetActiveGoalsAsync();

        goals.Should().ContainSingle().Which.Current.Should().Be(1,
            "Premium applies the exclusion, so the excluded book does not count");
    }
}
