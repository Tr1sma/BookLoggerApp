using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// CODE_REVIEW LOG-07: the paywall "first month" intro badge is driven by per-SKU offer data,
/// not shown for every Monthly card. No SKU has a confirmed intro offer yet, so the catalog must
/// report none.
/// </summary>
public class ProductCatalogTests
{
    private readonly ProductCatalog _catalog = new();

    [Theory]
    [InlineData(SubscriptionTier.Plus, BillingPeriod.Monthly)]
    [InlineData(SubscriptionTier.Premium, BillingPeriod.Monthly)]
    [InlineData(SubscriptionTier.Plus, BillingPeriod.Yearly)]
    [InlineData(SubscriptionTier.Premium, BillingPeriod.Yearly)]
    public void HasIntroductoryOffer_ReturnsFalse_WhenNoOfferConfigured(SubscriptionTier tier, BillingPeriod period)
    {
        _catalog.HasIntroductoryOffer(tier, period).Should().BeFalse();
    }

    [Fact]
    public void HasIntroductoryOffer_ReturnsFalse_ForUnknownCombination()
    {
        _catalog.HasIntroductoryOffer(SubscriptionTier.Free, BillingPeriod.Monthly).Should().BeFalse();
    }
}
