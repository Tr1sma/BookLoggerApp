using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Infrastructure;
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
            .Returns(new PromoCodeRedemptionResult(true, "Plus unlocked for 30 days.", activation));
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
    public async Task RedeemPromoAsync_OnFailure_SetsBannerAndDoesNotShowCelebration()
    {
        _promoCodes.RedeemAsync("BOGUS", Arg.Any<CancellationToken>())
            .Returns(new PromoCodeRedemptionResult(false, "Unknown promo code."));
        var vm = CreateVm();
        vm.PromoCodeInput = "BOGUS";

        await vm.RedeemPromoAsync();

        vm.ShowCelebration.Should().BeFalse();
        vm.Banner.Should().Be("Unknown promo code.");
        vm.PromoCodeInput.Should().Be("BOGUS");
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
    public void DismissCelebration_ClearsShowCelebration()
    {
        var vm = CreateVm();
        vm.ShowCelebration = true;

        vm.DismissCelebration();

        vm.ShowCelebration.Should().BeFalse();
    }
}
