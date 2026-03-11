using FluentAssertions;
using Xunit;

using BookLoggerApp.Core.Enums;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;

namespace BookLoggerApp.Tests.Services;

public class AdServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory;
    private readonly string _databaseName;

    public AdServiceTests()
    {
        _databaseName = Guid.NewGuid().ToString();
        _contextFactory = new TestDbContextFactory(_databaseName);
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    // --- MockAdService behavior ---

    [Fact]
    public void IsBannerVisible_DefaultsFalse()
    {
        var service = new MockAdService();

        service.IsBannerVisible.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldIncrementCallCount()
    {
        var service = new MockAdService();

        await service.InitializeAsync();

        service.InitializeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InitializeAsync_WhenConfiguredToFail_ShouldThrow()
    {
        var service = new MockAdService { ShouldInitializeSuccessfully = false };

        var act = () => service.InitializeAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void SimulateBannerVisibilityChange_ShouldRaiseEvent()
    {
        var service = new MockAdService();
        bool eventRaised = false;
        bool eventValue = false;
        service.BannerVisibilityChanged += (_, visible) =>
        {
            eventRaised = true;
            eventValue = visible;
        };

        service.SimulateBannerVisibilityChange(true);

        eventRaised.Should().BeTrue();
        eventValue.Should().BeTrue();
        service.IsBannerVisible.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAdVisibilityAsync_ShouldIncrementCallCount()
    {
        var service = new MockAdService();

        await service.RefreshAdVisibilityAsync();

        service.RefreshCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ShowPrivacyOptionsAsync_ShouldIncrementCallCount()
    {
        var service = new MockAdService();

        await service.ShowPrivacyOptionsAsync();

        service.ShowPrivacyCallCount.Should().Be(1);
    }

    // --- Subscription integration (verifies FeatureFlag.AdFree gating) ---

    [Fact]
    public async Task FreeTier_ShouldNotHaveAdFreeAccess()
    {
        var subscriptionService = new SubscriptionService(_contextFactory);

        var hasAdFree = await subscriptionService.HasFeatureAccessAsync(FeatureFlag.AdFree);

        hasAdFree.Should().BeFalse("Free tier users should see ads");
    }

    [Fact]
    public async Task PremiumTier_ShouldHaveAdFreeAccess()
    {
        var subscriptionService = new SubscriptionService(_contextFactory);
        await subscriptionService.UpdateTierAsync(SubscriptionTier.Premium);

        var hasAdFree = await subscriptionService.HasFeatureAccessAsync(FeatureFlag.AdFree);

        hasAdFree.Should().BeTrue("Premium tier users should not see ads");
    }

    [Fact]
    public async Task TierChanged_FromFreeToPremium_ShouldFireEvent()
    {
        var subscriptionService = new SubscriptionService(_contextFactory);
        await subscriptionService.GetCurrentTierAsync();

        bool tierChanged = false;
        subscriptionService.TierChanged += (_, _) => tierChanged = true;

        await subscriptionService.UpdateTierAsync(SubscriptionTier.Premium);

        tierChanged.Should().BeTrue(
            "TierChanged should fire when upgrading, which triggers ad hide");
    }

    [Fact]
    public async Task TierChanged_FromPremiumToFree_ShouldFireEvent()
    {
        var subscriptionService = new SubscriptionService(_contextFactory);
        await subscriptionService.UpdateTierAsync(SubscriptionTier.Premium);

        bool tierChanged = false;
        subscriptionService.TierChanged += (_, _) => tierChanged = true;

        await subscriptionService.UpdateTierAsync(SubscriptionTier.Free);

        tierChanged.Should().BeTrue(
            "TierChanged should fire when downgrading, which triggers ad show");
    }
}
