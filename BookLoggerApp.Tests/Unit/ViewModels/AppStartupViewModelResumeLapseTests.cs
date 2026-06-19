using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

/// <summary>
/// CODE_REVIEW BUG-02: app-resume must only lapse genuine Play purchases that Google Play
/// no longer returns — never an active promo grant (which is not a Play purchase and so never
/// appears in QueryActivePurchasesAsync).
/// </summary>
public class AppStartupViewModelResumeLapseTests
{
    private readonly IAppVersionService _appVersionService = Substitute.For<IAppVersionService>();
    private readonly IChangelogService _changelogService = Substitute.For<IChangelogService>();
    private readonly IAppUpdateService _appUpdateService = Substitute.For<IAppUpdateService>();
    private readonly IOnboardingService _onboardingService = Substitute.For<IOnboardingService>();
    private readonly IAppSettingsProvider _settingsProvider = Substitute.For<IAppSettingsProvider>();
    private readonly IEntitlementService _entitlementService = Substitute.For<IEntitlementService>();
    private readonly IBillingService _billingService = Substitute.For<IBillingService>();

    public AppStartupViewModelResumeLapseTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();

        _appVersionService.CurrentVersion.Returns("0.8.0");
        _appVersionService.LastSeenChangelogVersion.Returns("0.8.0");
        _appUpdateService.GetStateAsync(Arg.Any<CancellationToken>()).Returns(AppUpdateState.Unsupported);
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { PrivacyBannerDismissed = true });
        _onboardingService.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(CreateSnapshot());
        _onboardingService.RefreshSnapshotAsync(Arg.Any<CancellationToken>()).Returns(CreateSnapshot());

        _billingService.IsConnected.Returns(true);
        _billingService.QueryActivePurchasesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PurchaseResult>());
    }

    private AppStartupViewModel CreateVm() => new(
        _appVersionService,
        _changelogService,
        _appUpdateService,
        _onboardingService,
        _settingsProvider,
        _entitlementService,
        _billingService);

    [Fact]
    public async Task HandleAppResumedAsync_DoesNotLapse_ActivePromoGrant()
    {
        // A promo grant has no ProductId/PurchaseToken and a future PromoExpiresAt.
        _entitlementService.CurrentTier.Returns(SubscriptionTier.Plus);
        _entitlementService.CurrentEntitlement.Returns(new UserEntitlement
        {
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            ProductId = null,
            PurchaseToken = null,
            PromoCodeRedeemed = "BH-LAUNCH",
            PromoExpiresAt = DateTime.UtcNow.AddDays(60)
        });

        AppStartupViewModel vm = CreateVm();
        await vm.InitializeAsync();

        await vm.HandleAppResumedAsync();

        await _entitlementService.DidNotReceive().ApplyLapseAsync("expired", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAppResumedAsync_Lapses_PlayPurchase_WhenPlayReturnsNothing()
    {
        // A genuine Play subscription that Google Play no longer returns must lapse.
        _entitlementService.CurrentTier.Returns(SubscriptionTier.Premium);
        _entitlementService.CurrentEntitlement.Returns(new UserEntitlement
        {
            Tier = SubscriptionTier.Premium,
            BillingPeriod = BillingPeriod.Monthly,
            ProductId = "premium_monthly",
            PurchaseToken = "play-token",
            PromoExpiresAt = null
        });

        AppStartupViewModel vm = CreateVm();
        await vm.InitializeAsync();

        await vm.HandleAppResumedAsync();

        await _entitlementService.Received().ApplyLapseAsync("expired", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAppResumedAsync_DoesNotLapse_ExpiredPromoWithoutPlayPurchase()
    {
        // An expired promo (no Play token) is handled by the expiry path in RefreshAsync,
        // not the resume "Play stopped returning it" branch — so resume must not force-lapse it.
        _entitlementService.CurrentTier.Returns(SubscriptionTier.Plus);
        _entitlementService.CurrentEntitlement.Returns(new UserEntitlement
        {
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            ProductId = null,
            PurchaseToken = null,
            PromoCodeRedeemed = "BH-BETA2026",
            PromoExpiresAt = DateTime.UtcNow.AddDays(-1)
        });

        AppStartupViewModel vm = CreateVm();
        await vm.InitializeAsync();

        await vm.HandleAppResumedAsync();

        await _entitlementService.DidNotReceive().ApplyLapseAsync("expired", Arg.Any<CancellationToken>());
    }

    private static OnboardingSnapshot CreateSnapshot() => new()
    {
        FlowVersion = OnboardingMissionCatalog.CurrentFlowVersion,
        IntroStepCount = OnboardingMissionCatalog.IntroStepCount,
        CurrentIntroStep = 0,
        IntroStatus = OnboardingIntroStatus.Completed,
        ShouldShowIntro = false
    };
}
