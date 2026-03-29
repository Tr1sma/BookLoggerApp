using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class WidgetDataServiceTests
{
    [Fact]
    public async Task GetWidgetDataAsync_ShouldReturnCurrentBookStreakAndDailyGoal()
    {
        // Arrange
        var bookService = Substitute.For<IBookService>();
        var progressService = Substitute.For<IProgressService>();
        var goalService = Substitute.For<IGoalService>();

        var now = DateTime.UtcNow;
        bookService.GetByStatusAsync(ReadingStatus.Reading, Arg.Any<CancellationToken>())
            .Returns(new List<Book>
            {
                new()
                {
                    Title = "Older Book",
                    DateStarted = now.AddDays(-2),
                    CurrentPage = 40,
                    PageCount = 200
                },
                new()
                {
                    Title = "Newest Book",
                    DateStarted = now.AddDays(-1),
                    CurrentPage = 75,
                    PageCount = 300
                }
            });

        progressService.GetCurrentStreakAsync(Arg.Any<CancellationToken>()).Returns(7);
        goalService.GetActiveDailyGoalAsync(Arg.Any<CancellationToken>())
            .Returns(new ReadingGoal
            {
                Title = "Daily Pages",
                Type = GoalType.Pages,
                Current = 20,
                Target = 50
            });

        var service = new WidgetDataService(bookService, progressService, goalService);

        // Act
        var result = await service.GetWidgetDataAsync();

        // Assert
        result.CurrentBookTitle.Should().Be("Newest Book");
        result.CurrentBookProgressPercent.Should().Be(25);
        result.CurrentBookProgressText.Should().Be("75/300 Seiten");
        result.StreakDays.Should().Be(7);
        result.DailyGoalTitle.Should().Be("Daily Pages");
        result.DailyGoalProgressPercent.Should().Be(40);
        result.DailyGoalProgressText.Should().Be("20/50 Seiten");
    }

    [Fact]
    public async Task GetWidgetDataAsync_ShouldReturnFallbackTextWhenNoBookAndNoGoal()
    {
        // Arrange
        var bookService = Substitute.For<IBookService>();
        var progressService = Substitute.For<IProgressService>();
        var goalService = Substitute.For<IGoalService>();

        bookService.GetByStatusAsync(ReadingStatus.Reading, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Book>());
        progressService.GetCurrentStreakAsync(Arg.Any<CancellationToken>()).Returns(0);
        goalService.GetActiveDailyGoalAsync(Arg.Any<CancellationToken>())
            .Returns((ReadingGoal?)null);

        var service = new WidgetDataService(bookService, progressService, goalService);

        // Act
        var result = await service.GetWidgetDataAsync();

        // Assert
        result.CurrentBookTitle.Should().BeNull();
        result.CurrentBookProgressPercent.Should().Be(0);
        result.CurrentBookProgressText.Should().Be("Kein aktives Buch");
        result.StreakDays.Should().Be(0);
        result.DailyGoalTitle.Should().BeNull();
        result.DailyGoalProgressPercent.Should().Be(0);
        result.DailyGoalProgressText.Should().Be("Kein Tagesziel");
    }
}
