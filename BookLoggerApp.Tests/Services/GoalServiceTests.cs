using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class GoalServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GoalService _service;

    public GoalServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new GoalService(_unitOfWork);
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

    [Fact]
    public async Task ExcludeBookFromGoalAsync_ShouldExcludeBook()
    {
        // Arrange
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 5 books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book = new Book
        {
            Title = "Test Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow
        };
        await _unitOfWork.Books.AddAsync(book);
        await _unitOfWork.SaveChangesAsync();

        // Act
        await _service.ExcludeBookFromGoalAsync(goal.Id, book.Id);

        // Assert
        var exclusions = await _service.GetExcludedBooksAsync(goal.Id);
        exclusions.Should().HaveCount(1);
        exclusions.First().BookId.Should().Be(book.Id);
    }

    [Fact]
    public async Task IncludeBookInGoalAsync_ShouldRemoveExclusion()
    {
        // Arrange
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 5 books",
            Type = GoalType.Books,
            Target = 5,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book = new Book
        {
            Title = "Test Book",
            Author = "Author",
            Status = ReadingStatus.Completed,
            DateCompleted = DateTime.UtcNow
        };
        await _unitOfWork.Books.AddAsync(book);
        await _unitOfWork.SaveChangesAsync();

        await _service.ExcludeBookFromGoalAsync(goal.Id, book.Id);

        // Act
        await _service.IncludeBookInGoalAsync(goal.Id, book.Id);

        // Assert
        var exclusions = await _service.GetExcludedBooksAsync(goal.Id);
        exclusions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveGoalsAsync_ShouldNotCountExcludedBooks()
    {
        // Arrange: Create a goal and two completed books within the goal's date range
        var goal = await _service.AddAsync(new ReadingGoal
        {
            Title = "Read 3 books",
            Type = GoalType.Books,
            Target = 3,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30)
        });

        var book1 = new Book { Title = "Book 1", Author = "A", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book2 = new Book { Title = "Book 2", Author = "B", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        var book3 = new Book { Title = "Book 3", Author = "C", Status = ReadingStatus.Completed, DateCompleted = DateTime.UtcNow };
        await _unitOfWork.Books.AddAsync(book1);
        await _unitOfWork.Books.AddAsync(book2);
        await _unitOfWork.Books.AddAsync(book3);
        await _unitOfWork.SaveChangesAsync();

        // TODO(human): Exclude one book and verify the goal progress reflects the exclusion
    }
}
