using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Infrastructure;
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
    private readonly IOnboardingService _onboardingService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly AppStartupViewModel _viewModel;

    public AppStartupViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();

        _appVersionService = Substitute.For<IAppVersionService>();
        _changelogService = Substitute.For<IChangelogService>();
        _appUpdateService = Substitute.For<IAppUpdateService>();
        _onboardingService = Substitute.For<IOnboardingService>();
        _settingsProvider = Substitute.For<IAppSettingsProvider>();
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings { PrivacyBannerDismissed = true });

        _appVersionService.CurrentVersion.Returns("0.8.0");
        // Default to "user has already seen this version's changelog" so tests that don't
        // care about the changelog aren't poisoned by the first-launch path. Tests that
        // expect the changelog to surface override this with .Returns((string?)null).
        _appVersionService.LastSeenChangelogVersion.Returns("0.8.0");
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
        _changelogService.GetUnreleasedChangesAsync(Arg.Any<CancellationToken>())
            .Returns((ChangelogRelease?)null);
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(AppUpdateState.Unsupported);
        _onboardingService.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(CreateSnapshot());
        _onboardingService.RefreshSnapshotAsync(Arg.Any<CancellationToken>()).Returns(CreateSnapshot());

        _viewModel = new AppStartupViewModel(
            _appVersionService,
            _changelogService,
            _appUpdateService,
            _onboardingService,
            _settingsProvider);
    }

    [Fact]
    public async Task InitializeAsync_ShouldShowChangelog_WhenCurrentVersionIsFirstLaunch()
    {
        // Arrange
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(true);
        _appVersionService.LastSeenChangelogVersion.Returns((string?)null);

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
        _appVersionService.LastSeenChangelogVersion.Returns((string?)null);
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

    [Fact]
    public async Task InitializeAsync_ShouldShowOnboarding_WhenOnboardingIsNotCompleted()
    {
        // Arrange
        _onboardingService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSnapshot(shouldShowIntro: true, introStatus: OnboardingIntroStatus.NotStarted));

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.IsOnboardingVisible.Should().BeTrue();
        _viewModel.IsChangelogVisible.Should().BeFalse();
    }

    [Fact]
    public async Task SkipOnboardingAsync_ShouldCompleteIntroAndHideOverlay()
    {
        // Arrange
        _onboardingService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSnapshot(shouldShowIntro: true, introStatus: OnboardingIntroStatus.NotStarted));
        _onboardingService.CompleteIntroAsync(true, Arg.Any<CancellationToken>())
            .Returns(CreateSnapshot());

        await _viewModel.InitializeAsync();

        // Act
        await _viewModel.SkipOnboardingAsync();

        // Assert
        await _onboardingService.Received(1).CompleteIntroAsync(true, Arg.Any<CancellationToken>());
        _viewModel.IsOnboardingVisible.Should().BeFalse();
    }

    [Fact]
    public async Task HandleBackAsync_ShouldShowSkipConfirmation_WhenOnboardingIsOnFirstStep()
    {
        // Arrange
        _onboardingService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSnapshot(shouldShowIntro: true, introStatus: OnboardingIntroStatus.NotStarted));

        await _viewModel.InitializeAsync();

        // Act
        var handled = await _viewModel.HandleBackAsync();

        // Assert
        handled.Should().BeTrue();
        _viewModel.IsOnboardingSkipConfirmationVisible.Should().BeTrue();
    }

    [Fact]
    public async Task HandleBackAsync_ShouldRetreatIntro_WhenCurrentStepIsGreaterThanZero()
    {
        // Arrange
        _onboardingService.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSnapshot(shouldShowIntro: true, currentStep: 2, introStatus: OnboardingIntroStatus.InProgress));
        _onboardingService.RetreatIntroAsync(Arg.Any<CancellationToken>())
            .Returns(CreateSnapshot(shouldShowIntro: true, currentStep: 1, introStatus: OnboardingIntroStatus.InProgress));

        await _viewModel.InitializeAsync();

        // Act
        var handled = await _viewModel.HandleBackAsync();

        // Assert
        handled.Should().BeTrue();
        _viewModel.OnboardingCurrentStep.Should().Be(1);
        await _onboardingService.Received(1).RetreatIntroAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_ShouldFallbackToUnreleased_WhenNoExactVersionMatch()
    {
        // Arrange
        _appVersionService.CurrentVersion.Returns("0.9.2");
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(true);
        _appVersionService.LastSeenChangelogVersion.Returns((string?)null);
        _changelogService.GetReleaseHistoryAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ChangelogRelease
            {
                Version = "0.9.0",
                DisplayVersion = "0.9.0",
                Sections = new[] { new ChangelogSection { Title = "Hinzugefügt", Entries = new[] { "Altes Feature" } } }
            }
        });
        _changelogService.GetUnreleasedChangesAsync(Arg.Any<CancellationToken>()).Returns(new ChangelogRelease
        {
            Version = ChangelogParser.UnreleasedVersion,
            DisplayVersion = "Unveröffentlicht",
            Sections = new[] { new ChangelogSection { Title = "Hinzugefügt", Entries = new[] { "Neues Feature" } } }
        });

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.IsChangelogVisible.Should().BeTrue();
        _viewModel.CurrentRelease.Should().NotBeNull();
        _viewModel.CurrentRelease!.Version.Should().Be("0.9.2");
        _viewModel.CurrentRelease.DisplayVersion.Should().Be("0.9.2");
        _viewModel.CurrentRelease.Sections[0].Entries.Should().Contain("Neues Feature");
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotShowChangelog_WhenNoMatchAndUnreleasedIsEmpty()
    {
        // Arrange
        _appVersionService.CurrentVersion.Returns("0.9.2");
        _appVersionService.IsFirstLaunchForCurrentVersion.Returns(true);
        _appVersionService.LastSeenChangelogVersion.Returns((string?)null);
        _changelogService.GetReleaseHistoryAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ChangelogRelease
            {
                Version = "0.9.0",
                DisplayVersion = "0.9.0",
                Sections = new[] { new ChangelogSection { Title = "Hinzugefügt", Entries = new[] { "Altes Feature" } } }
            }
        });
        _changelogService.GetUnreleasedChangesAsync(Arg.Any<CancellationToken>()).Returns(new ChangelogRelease
        {
            Version = ChangelogParser.UnreleasedVersion,
            DisplayVersion = "Unveröffentlicht",
            Sections = Array.Empty<ChangelogSection>()
        });

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.IsChangelogVisible.Should().BeFalse();
    }

    // ── BUG-02 resume-lapse promo-grant tests ─────────────────────────────

    /// <summary>
    /// Helper: creates a VM with optional entitlement + billing services injected.
    /// All shared constructor dependencies come from the class-level fields.
    /// </summary>
    private AppStartupViewModel BuildVmWithBilling(
        IEntitlementService entitlementService,
        IBillingService billingService)
    {
        return new AppStartupViewModel(
            _appVersionService, _changelogService, _appUpdateService,
            _onboardingService, _settingsProvider,
            entitlementService, billingService);
    }

    [Fact]
    public async Task HandleAppResumedAsync_does_not_lapse_when_active_promo_grant_is_present()
    {
        // Arrange
        IEntitlementService entitlementService = Substitute.For<IEntitlementService>();
        IBillingService billingService = Substitute.For<IBillingService>();

        entitlementService.CurrentTier.Returns(SubscriptionTier.Plus);
        entitlementService.CurrentEntitlement.Returns(new UserEntitlement
        {
            Id = Guid.NewGuid(),
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            PromoExpiresAt = DateTime.UtcNow.AddDays(7)  // active promo — not from Play
        });

        billingService.IsConnected.Returns(true);
        billingService.QueryActivePurchasesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PurchaseResult>>(Array.Empty<PurchaseResult>()));

        AppStartupViewModel vm = BuildVmWithBilling(entitlementService, billingService);
        await vm.InitializeAsync();

        // Act
        await vm.HandleAppResumedAsync();

        // Assert — promo is active: no lapse, even though Play returned no purchases
        await entitlementService.DidNotReceive()
            .ApplyLapseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAppResumedAsync_still_lapses_when_subscription_cancelled_and_no_promo()
    {
        // Arrange — regular Play subscription, cancelled (no active purchases, no promo)
        IEntitlementService entitlementService = Substitute.For<IEntitlementService>();
        IBillingService billingService = Substitute.For<IBillingService>();

        entitlementService.CurrentTier.Returns(SubscriptionTier.Plus);
        entitlementService.CurrentEntitlement.Returns(new UserEntitlement
        {
            Id = Guid.NewGuid(),
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            ProductId = "bh_plus_monthly",
            PromoExpiresAt = null  // no active promo
        });

        billingService.IsConnected.Returns(true);
        billingService.QueryActivePurchasesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PurchaseResult>>(Array.Empty<PurchaseResult>()));

        AppStartupViewModel vm = BuildVmWithBilling(entitlementService, billingService);
        await vm.InitializeAsync();

        // Act
        await vm.HandleAppResumedAsync();

        // Assert — no promo, no Play purchase → lapse should fire
        await entitlementService.Received(1)
            .ApplyLapseAsync("expired", Arg.Any<CancellationToken>());
    }

    private static OnboardingSnapshot CreateSnapshot(
        bool shouldShowIntro = false,
        int currentStep = 0,
        OnboardingIntroStatus introStatus = OnboardingIntroStatus.Completed)
    {
        return new OnboardingSnapshot
        {
            FlowVersion = OnboardingMissionCatalog.CurrentFlowVersion,
            IntroStepCount = OnboardingMissionCatalog.IntroStepCount,
            CurrentIntroStep = currentStep,
            IntroStatus = introStatus,
            ShouldShowIntro = shouldShowIntro
        };
    }
}
