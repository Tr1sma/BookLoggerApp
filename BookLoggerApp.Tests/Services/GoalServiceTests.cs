using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class GoalServiceTests : IDisposable
{
    private readonly Infrastructure.Data.AppDbContext _context;
    private readonly ReadingGoalRepository _repository;
    private readonly GoalService _service;

    public GoalServiceTests()
    {
        _context = TestDbContext.Create();
        _repository = new ReadingGoalRepository(_context);
        _service = new GoalService(_repository);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task UpdateGoalProgressAsync_ShouldUpdateProgress()
    {
        // Arrange
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 100 pages",
            Type = GoalType.Pages,
            Target = 100,
            Current = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        // Act
        await _service.UpdateGoalProgressAsync(goal.Id, 50);

        // Assert
        var updated = await _service.GetByIdAsync(goal.Id);
        updated!.Current.Should().Be(50);
        updated.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateGoalProgressAsync_ShouldAutoCompleteWhenTargetReached()
    {
        // Arrange
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 100 pages",
            Type = GoalType.Pages,
            Target = 100,
            Current = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        // Act
        await _service.UpdateGoalProgressAsync(goal.Id, 100);

        // Assert
        var updated = await _service.GetByIdAsync(goal.Id);
        updated!.Current.Should().Be(100);
        updated.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveGoalsAsync_ShouldReturnOnlyActiveGoals()
    {
        // Arrange
        await _service.AddAsync(new ReadingGoal
        {
            Title = "Active Goal",
            Type = GoalType.Books,
            Target = 10,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            IsCompleted = false
        });
        await _service.AddAsync(new ReadingGoal
        {
            Title = "Completed Goal",
            Type = GoalType.Books,
            Target = 10,
            Current = 10,
            StartDate = DateTime.UtcNow.AddDays(-60),
            EndDate = DateTime.UtcNow.AddDays(-30),
            IsCompleted = true
        });
        await _service.AddAsync(new ReadingGoal
        {
            Title = "Expired Goal",
            Type = GoalType.Books,
            Target = 10,
            StartDate = DateTime.UtcNow.AddDays(-60),
            EndDate = DateTime.UtcNow.AddDays(-1),
            IsCompleted = false
        });

        // Act
        var activeGoals = await _service.GetActiveGoalsAsync();

        // Assert
        activeGoals.Should().HaveCount(1);
        activeGoals.First().Title.Should().Be("Active Goal");
    }

    [Fact]
    public async Task CheckAndCompleteGoalsAsync_ShouldCompleteReachedGoals()
    {
        // Arrange
        var goal1 = await _service.AddAsync(new ReadingGoal
        {
            Title = "Goal 1",
            Type = GoalType.Pages,
            Target = 100,
            Current = 100, // Reached target
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            IsCompleted = false
        });
        var goal2 = await _service.AddAsync(new ReadingGoal
        {
            Title = "Goal 2",
            Type = GoalType.Pages,
            Target = 100,
            Current = 50, // Not reached yet
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            IsCompleted = false
        });

        // Act
        await _service.CheckAndCompleteGoalsAsync();

        // Assert
        var updated1 = await _service.GetByIdAsync(goal1.Id);
        var updated2 = await _service.GetByIdAsync(goal2.Id);

        updated1!.IsCompleted.Should().BeTrue();
        updated2!.IsCompleted.Should().BeFalse();
    }
}
