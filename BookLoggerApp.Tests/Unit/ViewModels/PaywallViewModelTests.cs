using System.Globalization;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class PaywallViewModelTests
{
    private readonly IEntitlementService _entitlements;
    private readonly IPaywallCoordinator _coordinator;
    private readonly IPromoCodeService _promoCodes;
    private readonly IBillingService _billing;
    private readonly IProductCatalog _catalog;

    public PaywallViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _entitlements = Substitute.For<IEntitlementService>();
        _coordinator = Substitute.For<IPaywallCoordinator>();
        _promoCodes = Substitute.For<IPromoCodeService>();
        _billing = Substitute.For<IBillingService>();
        _catalog = Substitute.For<IProductCatalog>();

        _entitlements.CurrentTier.Returns(SubscriptionTier.Free);
        _billing.IsConnected.Returns(true);
    }

    private PaywallViewModel CreateVm() => new(
        _entitlements, _coordinator, _promoCodes, _billing, _catalog);

    [Fact]
    public async Task RedeemPromoAsync_OnSuccess_ShowsCelebrationAndClearsBanner()
    {
        PromoActivation activation = new(SubscriptionTier.Plus, BillingPeriod.Monthly, "BH-BETA2026", DateTime.UtcNow.AddDays(30));
        _promoCodes.RedeemAsync("BH-BETA2026", Arg.Any<CancellationToken>())
            .Returns(new PromoCodeRedemptionResult(true, "Promo_Success_Days", new object[] { SubscriptionTier.Plus, 30 }, activation));
        var vm = CreateVm();
        vm.PromoCodeInput = "BH-BETA2026";

        await vm.RedeemPromoAsync();

        vm.ShowCelebration.Should().BeTrue();
        vm.CelebrationHeadline.Should().Be("Successfully redeemed code");
        vm.CelebrationDetail.Should().Be("Plus unlocked for 30 days.");
        vm.Banner.Should().BeNull();
        vm.PromoCodeInput.Should().BeEmpty();
    }

    [Fact]
    public async Task RedeemPromoAsync_OnFailure_SetsPromoMessageAndDoesNotShowCelebration()
    {
        _promoCodes.RedeemAsync("BOGUS", Arg.Any<CancellationToken>())
            .Returns(new PromoCodeRedemptionResult(false, "Promo_Unknown", Array.Empty<object>()));
        var vm = CreateVm();
        vm.PromoCodeInput = "BOGUS";

        await vm.RedeemPromoAsync();

        vm.ShowCelebration.Should().BeFalse();
        // The message belongs next to the input, not in the header banner several screens up.
        vm.PromoMessage.Should().Be("Unknown promo code.");
        vm.Banner.Should().BeNull();
        vm.ShowPlayStoreRedeem.Should().BeFalse();
        vm.PromoCodeInput.Should().Be("BOGUS");
    }

    [Fact]
    public async Task RedeemPromoAsync_PlayStoreCode_OffersPlayStoreRedemption()
    {
        _promoCodes.RedeemAsync("MKZYHKL3UQR3SU8MTDB1RKR", Arg.Any<CancellationToken>())
            .Returns(new PromoCodeRedemptionResult(
                false, "Promo_PlayStoreCode", Array.Empty<object>(), null, RequiresPlayStore: true));
        var vm = CreateVm();
        vm.PromoCodeInput = "MKZYHKL3UQR3SU8MTDB1RKR";

        await vm.RedeemPromoAsync();

        vm.ShowPlayStoreRedeem.Should().BeTrue();
        vm.PromoMessage.Should().Be("That looks like a Google Play code. Redeem it in the Play Store.");
        vm.ShowCelebration.Should().BeFalse();
        // The code stays put — the user has to copy it into the Play Store.
        vm.PromoCodeInput.Should().Be("MKZYHKL3UQR3SU8MTDB1RKR");
    }

    [Fact]
    public async Task OpenPlayStoreRedeemAsync_LaunchesRedeemFlow()
    {
        _billing.LaunchRedeemPromoFlowAsync(Arg.Any<CancellationToken>()).Returns(true);
        var vm = CreateVm();

        await vm.OpenPlayStoreRedeemAsync();

        await _billing.Received(1).LaunchRedeemPromoFlowAsync(Arg.Any<CancellationToken>());
        vm.PromoMessage.Should().BeNull();
    }

    [Fact]
    public async Task OpenPlayStoreRedeemAsync_WhenLaunchFails_TellsTheUser()
    {
        _billing.LaunchRedeemPromoFlowAsync(Arg.Any<CancellationToken>()).Returns(false);
        var vm = CreateVm();

        await vm.OpenPlayStoreRedeemAsync();

        vm.PromoMessage.Should().Be("Could not open the Play Store. Redeem the code in the Play Store app.");
    }

    [Fact]
    public async Task RedeemPromoAsync_OnSuccess_ClearsPlayStoreOffer()
    {
        PromoActivation activation = new(SubscriptionTier.Plus, BillingPeriod.Monthly, "BH-BETA2026", DateTime.UtcNow.AddDays(30));
        _promoCodes.RedeemAsync("PLAYCODE123456", Arg.Any<CancellationToken>())
            .Returns(new PromoCodeRedemptionResult(
                false, "Promo_PlayStoreCode", Array.Empty<object>(), null, RequiresPlayStore: true));
        _promoCodes.RedeemAsync("BH-BETA2026", Arg.Any<CancellationToken>())
            .Returns(new PromoCodeRedemptionResult(true, "Promo_Success_Days", new object[] { SubscriptionTier.Plus, 30 }, activation));
        var vm = CreateVm();

        vm.PromoCodeInput = "PLAYCODE123456";
        await vm.RedeemPromoAsync();
        vm.ShowPlayStoreRedeem.Should().BeTrue();

        vm.PromoCodeInput = "BH-BETA2026";
        await vm.RedeemPromoAsync();

        vm.ShowPlayStoreRedeem.Should().BeFalse();
        vm.PromoMessage.Should().BeNull();
    }

    [Fact]
    public async Task PurchaseTierAsync_OnSuccess_ShowsCelebrationWithTierHeadline()
    {
        _catalog.GetProductId(SubscriptionTier.Plus, BillingPeriod.Yearly).Returns("plus_yearly");
        _billing.LaunchPurchaseFlowAsync("plus_yearly", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(BillingPurchaseOutcome.Success);
        var vm = CreateVm();

        await vm.PurchaseTierAsync(SubscriptionTier.Plus, BillingPeriod.Yearly);

        vm.ShowCelebration.Should().BeTrue();
        vm.CelebrationHeadline.Should().Be("Plus unlocked!");
        vm.CelebrationDetail.Should().Be("Thanks! Your subscription is active.");
        vm.Banner.Should().BeNull();
    }

    [Fact]
    public async Task PurchaseTierAsync_Lifetime_OnSuccess_UsesLifetimeDetail()
    {
        _catalog.GetProductId(SubscriptionTier.Premium, BillingPeriod.Lifetime).Returns("premium_lifetime");
        _billing.LaunchPurchaseFlowAsync("premium_lifetime", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(BillingPurchaseOutcome.Success);
        var vm = CreateVm();

        await vm.PurchaseTierAsync(SubscriptionTier.Premium, BillingPeriod.Lifetime);

        vm.ShowCelebration.Should().BeTrue();
        vm.CelebrationHeadline.Should().Be("Premium unlocked!");
        vm.CelebrationDetail.Should().Be("Thanks for going Lifetime — enjoy forever.");
    }

    [Fact]
    public async Task PurchaseTierAsync_OnFailure_SetsBannerAndDoesNotShowCelebration()
    {
        _catalog.GetProductId(SubscriptionTier.Plus, BillingPeriod.Monthly).Returns("plus_monthly");
        _billing.LaunchPurchaseFlowAsync("plus_monthly", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(BillingPurchaseOutcome.AlreadyOwned);
        var vm = CreateVm();

        await vm.PurchaseTierAsync(SubscriptionTier.Plus, BillingPeriod.Monthly);

        vm.ShowCelebration.Should().BeFalse();
        vm.Banner.Should().Be("You already own this subscription.");
    }

    [Fact]
    public async Task PurchaseTierAsync_WhenAlreadySubscribed_PassesOwnedPurchaseTokenForProration()
    {
        // BUG-12: switching from an owned subscription must pass the old token so Play prorates instead of AlreadyOwned.
        _entitlements.CurrentTier.Returns(SubscriptionTier.Plus);
        _entitlements.CurrentEntitlement.Returns(new UserEntitlement
        {
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            ProductId = "plus_monthly",
            PurchaseToken = "owned-token"
        });
        _catalog.GetProductId(SubscriptionTier.Premium, BillingPeriod.Yearly).Returns("premium_yearly");
        _billing.LaunchPurchaseFlowAsync("premium_yearly", "owned-token", Arg.Any<CancellationToken>())
            .Returns(BillingPurchaseOutcome.Success);
        var vm = CreateVm();

        await vm.PurchaseTierAsync(SubscriptionTier.Premium, BillingPeriod.Yearly);

        await _billing.Received(1).LaunchPurchaseFlowAsync("premium_yearly", "owned-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurchaseTierAsync_FreshPurchase_PassesNoOldToken()
    {
        _entitlements.CurrentTier.Returns(SubscriptionTier.Free);
        _catalog.GetProductId(SubscriptionTier.Plus, BillingPeriod.Yearly).Returns("plus_yearly");
        _billing.LaunchPurchaseFlowAsync("plus_yearly", null, Arg.Any<CancellationToken>())
            .Returns(BillingPurchaseOutcome.Success);
        var vm = CreateVm();

        await vm.PurchaseTierAsync(SubscriptionTier.Plus, BillingPeriod.Yearly);

        await _billing.Received(1).LaunchPurchaseFlowAsync("plus_yearly", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurchaseTierAsync_Lifetime_DoesNotPassOldToken_EvenWhenSubscribed()
    {
        // A managed Lifetime product cannot proration-replace a subscription.
        _entitlements.CurrentTier.Returns(SubscriptionTier.Plus);
        _entitlements.CurrentEntitlement.Returns(new UserEntitlement
        {
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            ProductId = "plus_monthly",
            PurchaseToken = "owned-token"
        });
        _catalog.GetProductId(SubscriptionTier.Premium, BillingPeriod.Lifetime).Returns("premium_lifetime");
        _billing.LaunchPurchaseFlowAsync("premium_lifetime", null, Arg.Any<CancellationToken>())
            .Returns(BillingPurchaseOutcome.Success);
        var vm = CreateVm();

        await vm.PurchaseTierAsync(SubscriptionTier.Premium, BillingPeriod.Lifetime);

        await _billing.Received(1).LaunchPurchaseFlowAsync("premium_lifetime", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurchaseTierAsync_WhenAlreadyInProgress_IgnoresReentrantCall()
    {
        // BUG-18: double-tapping buy must not launch two flows; the reentrancy guard short-circuits the second.
        _catalog.GetProductId(SubscriptionTier.Plus, BillingPeriod.Yearly).Returns("plus_yearly");
        var gate = new TaskCompletionSource<BillingPurchaseOutcome>();
        _billing.LaunchPurchaseFlowAsync("plus_yearly", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(gate.Task);
        var vm = CreateVm();

        // First call flips IsPurchaseInProgress and parks on the gated flow.
        Task first = vm.PurchaseTierAsync(SubscriptionTier.Plus, BillingPeriod.Yearly);
        vm.IsPurchaseInProgress.Should().BeTrue();

        // Second call (double-tap) must short-circuit on the guard.
        Task second = vm.PurchaseTierAsync(SubscriptionTier.Plus, BillingPeriod.Yearly);

        // Release the gate and let both settle.
        gate.SetResult(BillingPurchaseOutcome.Success);
        await Task.WhenAll(first, second);

        await _billing.Received(1).LaunchPurchaseFlowAsync("plus_yearly", Arg.Any<string?>(), Arg.Any<CancellationToken>());
        vm.IsPurchaseInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task PurchaseTierAsync_OnSuccess_UsesLocalizedGermanCelebration()
    {
        // UX-04: paywall celebration/banner copy must come from resx so a German user sees German text.
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("de");
            _catalog.GetProductId(SubscriptionTier.Plus, BillingPeriod.Yearly).Returns("plus_yearly");
            _billing.LaunchPurchaseFlowAsync("plus_yearly", Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(BillingPurchaseOutcome.Success);
            var vm = CreateVm();

            await vm.PurchaseTierAsync(SubscriptionTier.Plus, BillingPeriod.Yearly);

            vm.CelebrationHeadline.Should().Be("Plus freigeschaltet!");
            vm.CelebrationDetail.Should().Be("Danke! Dein Abo ist aktiv.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public async Task PurchaseTierAsync_AlreadyOwned_UsesLocalizedGermanBanner()
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("de");
            _catalog.GetProductId(SubscriptionTier.Plus, BillingPeriod.Monthly).Returns("plus_monthly");
            _billing.LaunchPurchaseFlowAsync("plus_monthly", Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(BillingPurchaseOutcome.AlreadyOwned);
            var vm = CreateVm();

            await vm.PurchaseTierAsync(SubscriptionTier.Plus, BillingPeriod.Monthly);

            vm.Banner.Should().Be("Du besitzt dieses Abo bereits.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void DismissCelebration_ClearsShowCelebration()
    {
        var vm = CreateVm();
        vm.ShowCelebration = true;

        vm.DismissCelebration();

        vm.ShowCelebration.Should().BeFalse();
    }
}
