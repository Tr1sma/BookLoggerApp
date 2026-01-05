using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class DashboardViewModelTests
{
    private readonly IBookService _bookService;
    private readonly IProgressService _progressService;
    private readonly IGoalService _goalService;
    private readonly IPlantService _plantService;
    private readonly IStatsService _statsService;
    private readonly DashboardViewModel _viewModel;

    public DashboardViewModelTests()
    {
        _bookService = Substitute.For<IBookService>();
        _progressService = Substitute.For<IProgressService>();
        _goalService = Substitute.For<IGoalService>();
        _plantService = Substitute.For<IPlantService>();
        _statsService = Substitute.For<IStatsService>();

        _viewModel = new DashboardViewModel(
            _bookService,
            _progressService,
            _goalService,
            _plantService,
            _statsService
        );
    }

    [Fact]
    public async Task LoadAsync_Should_Populate_Dashboard_Data()
    {
        // Arrange
        var readingBook = new Book { Title = "Reading Book", Status = ReadingStatus.Reading };
        _bookService.GetByStatusAsync(ReadingStatus.Reading).Returns(new List<Book> { readingBook });

        var completedBook = new Book 
        { 
            Title = "Done Book", 
            Status = ReadingStatus.Completed, 
            DateCompleted = DateTime.UtcNow 
        };
        _bookService.GetByStatusAsync(ReadingStatus.Completed).Returns(new List<Book> { completedBook });

        var sessions = new List<ReadingSession>
        {
            new ReadingSession { Minutes = 30, PagesRead = 10, XpEarned = 50 }
        };
        _progressService.GetSessionsInRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(sessions);

        var goals = new List<ReadingGoal> { new ReadingGoal { Title = "Goal 1" } };
        _goalService.GetActiveGoalsAsync().Returns(goals);

        var plant = new UserPlant { Id = Guid.NewGuid() };
        _plantService.GetActivePlantAsync().Returns(plant);

        var recentSessions = new List<ReadingSession> 
        { 
            new ReadingSession { BookId = Guid.NewGuid() } 
        };
        _progressService.GetRecentSessionsAsync(5).Returns(recentSessions);

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.CurrentlyReading.Should().Be(readingBook);
        _viewModel.BooksReadThisWeek.Should().Be(1);
        _viewModel.MinutesReadThisWeek.Should().Be(30);
        _viewModel.PagesReadThisWeek.Should().Be(10);
        _viewModel.XpEarnedThisWeek.Should().Be(50);
        _viewModel.ActiveGoals.Should().HaveCount(1);
        _viewModel.ActivePlant.Should().Be(plant);
        _viewModel.RecentActivity.Should().HaveCount(1);
    }

    [Fact]
    public async Task WaterPlantAsync_Should_Call_PlantService()
    {
        // Arrange
        var plant = new UserPlant { Id = Guid.NewGuid() };
        _plantService.GetActivePlantAsync().Returns(plant);
        await _viewModel.LoadCommand.ExecuteAsync(null); // Load plant first

        // Act
        await _viewModel.WaterPlantCommand.ExecuteAsync(null);

        // Assert
        await _plantService.Received(1).WaterPlantAsync(plant.Id);
    }
}
