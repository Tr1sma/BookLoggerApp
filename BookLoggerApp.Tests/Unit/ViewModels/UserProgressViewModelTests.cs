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
        // 1000 XP: passes L1 (100) and L2 (400), stops before L3 (900) → Level 3, 500/900=55.55%
        var settings = new AppSettings
        {
            UserLevel = 1, // stale
            TotalXp = 1000
        };

        _mockSettingsProvider.GetSettingsAsync().Returns(settings);

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.CurrentLevel.Should().Be(3);
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
