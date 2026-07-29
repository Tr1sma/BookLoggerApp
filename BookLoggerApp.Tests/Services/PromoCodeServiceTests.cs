using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class PromoCodeServiceTests
{
    private readonly IEntitlementService _entitlements;
    private readonly PromoCodeService _service;

    public PromoCodeServiceTests()
    {
        _entitlements = Substitute.For<IEntitlementService>();
        _service = new PromoCodeService(_entitlements);
    }

    [Fact]
    public async Task RedeemAsync_KnownInAppCode_AppliesPromo()
    {
        PromoCodeRedemptionResult result = await _service.RedeemAsync("BH-BETA2026");

        result.Success.Should().BeTrue();
        result.RequiresPlayStore.Should().BeFalse();
        result.Activation.Should().NotBeNull();
        result.Activation!.GrantedTier.Should().Be(SubscriptionTier.Plus);
        await _entitlements.Received(1).ApplyPromoAsync(
            Arg.Is<PromoActivation>(p => p.Code == "BH-BETA2026"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RedeemAsync_EmptyCode_AsksForACode()
    {
        PromoCodeRedemptionResult result = await _service.RedeemAsync("   ");

        result.Success.Should().BeFalse();
        result.MessageKey.Should().Be("Promo_EnterCode");
        result.RequiresPlayStore.Should().BeFalse();
    }

    [Fact]
    public async Task RedeemAsync_UnknownCodeInOurNamespace_StaysUnknown()
    {
        // The hyphen puts it in our BH- namespace, so it's a typo — not a Play code.
        PromoCodeRedemptionResult result = await _service.RedeemAsync("BH-NOPE");

        result.Success.Should().BeFalse();
        result.MessageKey.Should().Be("Promo_Unknown");
        result.RequiresPlayStore.Should().BeFalse();
    }

    [Fact]
    public async Task RedeemAsync_PlayStoreCode_DefersToPlayWithoutGrantingAnything()
    {
        PromoCodeRedemptionResult result = await _service.RedeemAsync("MKZYHKL3UQR3SU8MTDB1RKR");

        result.Success.Should().BeFalse();
        result.MessageKey.Should().Be("Promo_PlayStoreCode");
        result.RequiresPlayStore.Should().BeTrue();
        result.Activation.Should().BeNull();
        await _entitlements.DidNotReceive().ApplyPromoAsync(
            Arg.Any<PromoActivation>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("mkzyhkl3uqr3su8mtdb1rkr")]     // lowercase — Play codes are shown uppercase but users paste anything
    [InlineData("  MKZYHKL3UQR3SU8MTDB1RKR  ")] // padded by a clipboard paste
    [InlineData("SUMMER2026")]                  // Play Console custom code — shorter, same alphabet
    public async Task RedeemAsync_PlayFormatVariants_AreRecognised(string code)
    {
        PromoCodeRedemptionResult result = await _service.RedeemAsync(code);

        result.RequiresPlayStore.Should().BeTrue();
        result.MessageKey.Should().Be("Promo_PlayStoreCode");
    }

    [Theory]
    [InlineData("not a code")] // spaces
    [InlineData("ABC-123!")]   // punctuation
    [InlineData("XY")]         // too short to be a voucher
    public async Task RedeemAsync_MalformedInput_IsATypoNotAPlayCode(string code)
    {
        PromoCodeRedemptionResult result = await _service.RedeemAsync(code);

        result.RequiresPlayStore.Should().BeFalse();
        result.MessageKey.Should().Be("Promo_Unknown");
    }
}
