using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using BookLoggerApp.Core.Enums;
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
    private readonly IShareCardService _shareCardService;
    private readonly IProgressService _progressService;
    private readonly IBookService _bookService;
    private readonly StatsViewModel _viewModel;

    public StatsViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _statsService = Substitute.For<IStatsService>();
        _settingsProvider = Substitute.For<IAppSettingsProvider>();
        _plantService = Substitute.For<IPlantService>();
        _shareCardService = Substitute.For<IShareCardService>();
        _progressService = Substitute.For<IProgressService>();
        _bookService = Substitute.For<IBookService>();

        _statsService.GetReadingTrendAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<DateTime, int>());
        _statsService.GetBooksByGenreAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<string, int>());
        _statsService.GetAllAverageRatingsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<RatingCategory, double>());
        _statsService.GetTopRatedBooksAsync(Arg.Any<int>(), ct: Arg.Any<CancellationToken>()).Returns(new List<BookRatingSummary>());

        _viewModel = new StatsViewModel(_statsService, _settingsProvider, _plantService, _shareCardService, _progressService, _bookService);
    }

    [Fact]
    public async Task LoadAsync_Should_Populate_Statistics()
    {
        // Arrange
        _statsService.GetTotalBooksReadAsync(Arg.Any<CancellationToken>()).Returns(5);
        _statsService.GetTotalPagesReadAsync(Arg.Any<CancellationToken>()).Returns(1000);
        _statsService.GetTotalMinutesReadAsync(Arg.Any<CancellationToken>()).Returns(600);
        _statsService.GetCurrentStreakAsync(Arg.Any<CancellationToken>()).Returns(3);
        _statsService.GetLongestStreakAsync(Arg.Any<CancellationToken>()).Returns(10);
        _statsService.GetAverageRatingAsync(Arg.Any<CancellationToken>()).Returns(4.5);
        
        var settings = new AppSettings { UserLevel = 2, TotalXp = 250, Coins = 100 };
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        
        _plantService.CalculateTotalXpBoostAsync(Arg.Any<CancellationToken>()).Returns(0.1m);
        _plantService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<UserPlant>());

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
        var settings = new AppSettings { UserLevel = 2, TotalXp = 175 };
        // L1=100 XP cumulative; CurrentLevelXp=75, NextLevelXp=400, so 18.75%.

        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _plantService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<UserPlant>());
        _plantService.CalculateTotalXpBoostAsync(Arg.Any<CancellationToken>()).Returns(0m);

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ProgressPercentage.Should().Be(18.75m);
        _viewModel.CurrentLevelXp.Should().Be(75);
        _viewModel.NextLevelXp.Should().Be(400);
    }

    [Fact]
    public async Task LoadAsync_WithStaleLevel_Should_Recalculate_Level()
    {
        // Arrange
        // Stored Level 1 but TotalXp=600 (costs 100/400/900) recalculates to Level 3.

        var settings = new AppSettings { UserLevel = 1, TotalXp = 600 };
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _plantService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<UserPlant>());
        _plantService.CalculateTotalXpBoostAsync(Arg.Any<CancellationToken>()).Returns(0m);

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.CurrentLevel.Should().Be(3);
        _viewModel.CurrentLevelXp.Should().Be(100);
        _viewModel.NextLevelXp.Should().Be(900);
        _viewModel.ProgressPercentage.Should().BeApproximately(11.11m, 0.01m);
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

    [Fact]
    public async Task LoadAsync_Should_Exclude_DeadPlants_FromPlantBoosts()
    {
        // Arrange
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        _plantService.CalculateTotalXpBoostAsync(Arg.Any<CancellationToken>()).Returns(0m);
        _plantService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<UserPlant>
        {
            new()
            {
                Name = "Dead Plant",
                Status = PlantStatus.Dead,
                CurrentLevel = 3,
                Species = new PlantSpecies
                {
                    Name = "Withered",
                    MaxLevel = 10,
                    XpBoostPercentage = 0.05m
                }
            }
        });

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.PlantBoosts.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_Should_Generate_PerLevel_XpValues_Not_Cumulative()
    {
        // Arrange
        var settings = new AppSettings { UserLevel = 3, TotalXp = 600, Coins = 0 };
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _plantService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<UserPlant>());
        _plantService.CalculateTotalXpBoostAsync(Arg.Any<CancellationToken>()).Returns(0m);

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert — XpRequired should be 100 × Level² (per-level), not cumulative
        _viewModel.LevelMilestones.Should().NotBeEmpty();

        foreach (var milestone in _viewModel.LevelMilestones)
        {
            int expectedXp = 100 * milestone.Level * milestone.Level;
            milestone.XpRequired.Should().Be(expectedXp,
                because: $"Level {milestone.Level} requires 100 × {milestone.Level}² = {expectedXp} XP");

            int expectedCoins = XpCalculator.CalculateCoinsForLevel(milestone.Level);
            milestone.CoinsReward.Should().Be(expectedCoins,
                because: $"Level {milestone.Level} should award {expectedCoins} coins");
        }
    }
}
