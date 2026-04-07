using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class AppStartupViewModelTests
{
    private readonly IAppVersionService _appVersionService;
    private readonly IChangelogService _changelogService;
    private readonly IAppUpdateService _appUpdateService;
    private readonly AppStartupViewModel _viewModel;

    public AppStartupViewModelTests()
    {
        _appVersionService = Substitute.For<IAppVersionService>();
        _changelogService = Substitute.For<IChangelogService>();
        _appUpdateService = Substitute.For<IAppUpdateService>();

        _appVersionService.CurrentVersion.Returns("0.8.0");
        _changelogService.GetReleaseHistoryAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ChangelogRelease
            {
                Version = "0.8.0",
                DisplayVersion = "0.8.0",
                ReleaseDate = "2026-04-07",
                Sections = new[]
                {
                    new ChangelogSection
                    {
                        Title = "Hinzugefügt",
                        Entries = new[] { "Neues Feature" }
                    }
                }
            }
        });
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(AppUpdateState.Unsupported);

        _viewModel = new AppStartupViewModel(
            _appVersionService,
            _changelogService,
            _appUpdateService);
    }

    [Fact]
    public async Task InitializeAsync_ShouldShowChangelog_WhenCurrentVersionIsFirstLaunch()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(true);

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.IsChangelogVisible.Should().BeTrue();
        _viewModel.CurrentRelease.Should().NotBeNull();
        _viewModel.CurrentRelease!.Version.Should().Be("0.8.0");
        _viewModel.IsUpdateAvailableVisible.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldQueueUpdatePrompt_BehindChangelog()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(true);
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(new AppUpdateState
        {
            IsSupported = true,
            IsUpdateAvailable = true,
            CanStartFlexibleUpdate = true
        });

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.IsChangelogVisible.Should().BeTrue();
        _viewModel.IsUpdateAvailableVisible.Should().BeFalse();

        await _viewModel.CloseChangelogAsync();

        _viewModel.IsUpdateAvailableVisible.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAppResumedAsync_ShouldShowDownloadedUpdatePrompt_WhenDownloadCompleted()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(false);
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(new AppUpdateState
        {
            IsSupported = true,
            IsUpdateDownloaded = true,
            IsUpdateInProgress = true
        });

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.IsUpdateReadyVisible.Should().BeTrue();
        _viewModel.IsUpdateAvailableVisible.Should().BeFalse();
    }

    [Fact]
    public async Task DismissUpdateAvailableAsync_ShouldHidePrompt_ForCurrentSession()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(false);
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(new AppUpdateState
        {
            IsSupported = true,
            IsUpdateAvailable = true,
            CanStartFlexibleUpdate = true
        });

        await _viewModel.InitializeAsync();

        // Act
        await _viewModel.DismissUpdateAvailableAsync();
        await _viewModel.HandleAppResumedAsync();

        // Assert
        _viewModel.IsUpdateAvailableVisible.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAppResumedAsync_ShouldNotShowAvailablePrompt_WhenUpdateIsAlreadyInProgress()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(false);
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(
            new AppUpdateState
            {
                IsSupported = true,
                IsUpdateAvailable = true,
                CanStartFlexibleUpdate = true
            },
            new AppUpdateState
            {
                IsSupported = true,
                IsUpdateAvailable = true,
                IsUpdateInProgress = true
            });

        await _viewModel.InitializeAsync();

        // Act
        await _viewModel.HandleAppResumedAsync();

        // Assert
        _viewModel.IsUpdateAvailableVisible.Should().BeFalse();
        _viewModel.IsUpdateReadyVisible.Should().BeFalse();
        _viewModel.UpdateState.IsUpdateInProgress.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAppResumedAsync_ShouldCaptureError_WhenUpdateRefreshFails()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(false);
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(AppUpdateState.Unsupported);

        await _viewModel.InitializeAsync();

        _appUpdateService
            .GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<AppUpdateState>(new InvalidOperationException("Play Store offline")));

        // Act
        await _viewModel.HandleAppResumedAsync();

        // Assert
        _viewModel.ErrorMessage.Should().Be("Failed to refresh app update state: Play Store offline");
    }

    [Fact]
    public async Task StartFlexibleUpdateAsync_ShouldHidePrompt_WhenFlowStarts()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(false);
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(new AppUpdateState
        {
            IsSupported = true,
            IsUpdateAvailable = true,
            CanStartFlexibleUpdate = true
        });
        _appUpdateService.StartFlexibleUpdateAsync(Arg.Any<CancellationToken>()).Returns(true);

        await _viewModel.InitializeAsync();

        // Act
        await _viewModel.StartFlexibleUpdateAsync();

        // Assert
        _viewModel.IsUpdateAvailableVisible.Should().BeFalse();
        await _appUpdateService.Received(1).StartFlexibleUpdateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartFlexibleUpdateAsync_ShouldKeepPromptVisible_WhenUpdateFlowDidNotStart()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(false);
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(new AppUpdateState
        {
            IsSupported = true,
            IsUpdateAvailable = true,
            CanStartFlexibleUpdate = true
        });
        _appUpdateService.StartFlexibleUpdateAsync(Arg.Any<CancellationToken>()).Returns(false);

        await _viewModel.InitializeAsync();

        // Act
        await _viewModel.StartFlexibleUpdateAsync();

        // Assert
        _viewModel.IsUpdateAvailableVisible.Should().BeTrue();
        await _appUpdateService.Received(1).StartFlexibleUpdateAsync(Arg.Any<CancellationToken>());
    }
}
