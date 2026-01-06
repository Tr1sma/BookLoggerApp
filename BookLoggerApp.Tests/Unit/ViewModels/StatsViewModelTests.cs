using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using System.Collections.ObjectModel;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class StatsViewModelTests
{
    private readonly IStatsService _statsService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IPlantService _plantService;
    private readonly StatsViewModel _viewModel;

    public StatsViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _statsService = Substitute.For<IStatsService>();
        _settingsProvider = Substitute.For<IAppSettingsProvider>();
        _plantService = Substitute.For<IPlantService>();

        _statsService.GetReadingTrendAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new Dictionary<DateTime, int>());
        _statsService.GetBooksByGenreAsync().Returns(new Dictionary<string, int>());
        _statsService.GetAllAverageRatingsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new Dictionary<RatingCategory, double>());
        _statsService.GetTopRatedBooksAsync(Arg.Any<int>()).Returns(new List<BookRatingSummary>());

        _viewModel = new StatsViewModel(_statsService, _settingsProvider, _plantService);
    }

    [Fact]
    public async Task LoadAsync_Should_Populate_Statistics()
    {
        // Arrange
        _statsService.GetTotalBooksReadAsync().Returns(5);
        _statsService.GetTotalPagesReadAsync().Returns(1000);
        _statsService.GetTotalMinutesReadAsync().Returns(600);
        _statsService.GetCurrentStreakAsync().Returns(3);
        _statsService.GetLongestStreakAsync().Returns(10);
        _statsService.GetAverageRatingAsync().Returns(4.5);
        
        var settings = new AppSettings { UserLevel = 2, TotalXp = 250, Coins = 100 };
        _settingsProvider.GetSettingsAsync().Returns(settings);
        
        _plantService.CalculateTotalXpBoostAsync().Returns(0.1m);
        _plantService.GetAllAsync().Returns(new List<UserPlant>());

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.TotalBooksRead.Should().Be(5);
        _viewModel.TotalPagesRead.Should().Be(1000);
        _viewModel.TotalMinutesRead.Should().Be(600);
        _viewModel.CurrentStreak.Should().Be(3);
        _viewModel.LongestStreak.Should().Be(10);
        _viewModel.AverageRating.Should().Be(4.5);
        
        _viewModel.CurrentLevel.Should().Be(2);
        _viewModel.TotalXp.Should().Be(250);
        _viewModel.TotalCoins.Should().Be(100);
    }

    [Fact]
    public async Task LoadAsync_Should_Calculate_Level_Progress_Correctly()
    {
        // Arrange
        var settings = new AppSettings { UserLevel = 2, TotalXp = 175 }; // Level 1 is 100xp, so 75xp into Level 2 (which needs 150xp total delta? No, formula is different)
        // Formula: Level 1 = 100 XP. Level 2 req 150 XP. Cumulative: L1=100.
        // wait, source code: GetXpForLevel(1) = 100. xpForPreviousLevels(level 2) loops i=1 to 1. sums GetXpForLevel(1) = 100.
        // CurrentLevelXp = TotalXp (175) - 100 = 75.
        // NextLevelXp = GetXpForLevel(2) = 100 * 1.5^1 = 150.
        // Percentage = 75 / 150 = 50%
        
        _settingsProvider.GetSettingsAsync().Returns(settings);
        _plantService.GetAllAsync().Returns(new List<UserPlant>());

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ProgressPercentage.Should().Be(50m);
        _viewModel.CurrentLevelXp.Should().Be(75);
        _viewModel.NextLevelXp.Should().Be(150);
    }

    [Fact]
    public async Task FilterTopBooksByCategoryAsync_Should_Update_TopRatedBooks()
    {
        // Arrange
        var topBooks = new List<BookRatingSummary> 
        { 
            new BookRatingSummary 
            { 
                Book = new Book { Title = "Book 1" }, 
                AverageRating = 5 
            } 
        };
        _statsService.GetTopRatedBooksAsync(10, RatingCategory.Plot).Returns(topBooks);

        // Act
        await _viewModel.FilterTopBooksByCategoryAsync(RatingCategory.Plot);

        // Assert
        _viewModel.TopRatedBooks.Should().HaveCount(1);
        _viewModel.TopRatedBooks.First().Book.Title.Should().Be("Book 1");
    }
}
