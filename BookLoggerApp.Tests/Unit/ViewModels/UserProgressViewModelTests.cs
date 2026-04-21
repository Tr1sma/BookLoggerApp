using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class UserProgressViewModelTests
{
    private readonly IAppSettingsProvider _mockSettingsProvider;
    private readonly UserProgressViewModel _viewModel;

    public UserProgressViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _mockSettingsProvider = Substitute.For<IAppSettingsProvider>();
        _viewModel = new UserProgressViewModel(_mockSettingsProvider);
    }

    [Fact]
    public async Task LoadAsync_WithStaleLevel_ShouldCalculateCorrectLevelAndProgress()
    {
        // Arrange
        // Scenario: User has enough XP for Level 4 (1000 XP), but stored UserLevel is stale (Level 1)
        var settings = new AppSettings
        {
            UserLevel = 1, // Stale
            TotalXp = 1000 
        };

        _mockSettingsProvider.GetSettingsAsync().Returns(settings);

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        // We expect the ViewModel to correct the level to 3 (based on previous trace: 1->100, 2->400, 3->900, 4->1600)
        // With 1000 XP, level is 3. 
        // 1000 XP total.
        // Level 1: 0-100 (100 cost)
        // Level 2: 100-500 (400 cost)
        // Level 3: 500-1400 (900 cost)
        // Wait, trace again:
        // L1: 100. Acc: 100.
        // L2: 400. Acc: 500.
        // L3: 900. Acc: 1400.
        // With 1000 XP:
        // 1000 >= 100 -> L2, rem 900.
        // 900 >= 400 -> L3, rem 500.
        // 500 < 900 -> Stop.
        // Result: Level 3.
        
        _viewModel.CurrentLevel.Should().Be(3);
        
        // Validation of Progress
        // XP accumulated in current level (3) is 500.
        // XP needed for next level (4) [cost of L3] is 900.
        // Progress: 500 / 900 * 100 = 55.55%
        
        _viewModel.CurrentLevelXp.Should().Be(500);
        _viewModel.NextLevelXp.Should().Be(900);
        _viewModel.ProgressPercentage.Should().BeApproximately(55.55m, 0.01m);
    }

    [Fact]
    public async Task LoadAsync_ZeroXp_Level1WithNoProgress()
    {
        _mockSettingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AppSettings { UserLevel = 1, TotalXp = 0 }));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.CurrentLevel.Should().Be(1);
        _viewModel.TotalXp.Should().Be(0);
        _viewModel.CurrentLevelXp.Should().Be(0);
        _viewModel.ProgressPercentage.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_ExactlyAtLevelBoundary_SetsClampedPercentage()
    {
        _mockSettingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AppSettings { UserLevel = 1, TotalXp = 100 }));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.CurrentLevel.Should().Be(2);
        _viewModel.ProgressPercentage.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task RefreshAsync_ReloadsSettings()
    {
        _mockSettingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AppSettings { UserLevel = 1, TotalXp = 0 }));

        await _viewModel.RefreshCommand.ExecuteAsync(null);

        await _mockSettingsProvider.Received().GetSettingsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_ServiceThrows_SetsErrorMessage()
    {
        _mockSettingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<AppSettings>>(_ => throw new InvalidOperationException("db err"));

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().NotBeNull();
        _viewModel.ErrorMessage!.Should().Contain("Failed to load user progress");
    }
}
