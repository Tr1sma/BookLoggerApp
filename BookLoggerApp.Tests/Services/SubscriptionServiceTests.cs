using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

using BookLoggerApp.Core.Enums;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;

namespace BookLoggerApp.Tests.Services;

public class SubscriptionServiceTests : IDisposable
{
    private readonly SubscriptionService _service;
    private readonly TestDbContextFactory _contextFactory;
    private readonly string _databaseName;

    public SubscriptionServiceTests()
    {
        _databaseName = Guid.NewGuid().ToString();
        _contextFactory = new TestDbContextFactory(_databaseName);
        _service = new SubscriptionService(_contextFactory);
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    // --- GetCurrentTierAsync ---

    [Fact]
    public async Task GetCurrentTierAsync_WithNoExistingRecord_ShouldReturnFree()
    {
        var tier = await _service.GetCurrentTierAsync();

        tier.Should().Be(SubscriptionTier.Free);
    }

    [Fact]
    public async Task GetCurrentTierAsync_AfterUpgrade_ShouldReturnPremium()
    {
        await _service.UpdateTierAsync(SubscriptionTier.Premium);

        var tier = await _service.GetCurrentTierAsync();

        tier.Should().Be(SubscriptionTier.Premium);
    }

    [Fact]
    public async Task GetCurrentTierAsync_WithExpiredPremium_ShouldReturnFree()
    {
        await _service.UpdateTierAsync(
            SubscriptionTier.Premium,
            expiresAt: DateTime.UtcNow.AddDays(-1));

        _service.InvalidateCache();

        var tier = await _service.GetCurrentTierAsync();

        tier.Should().Be(SubscriptionTier.Free);
    }

    [Fact]
    public async Task GetCurrentTierAsync_WithActivePremium_ShouldReturnPremium()
    {
        await _service.UpdateTierAsync(
            SubscriptionTier.Premium,
            expiresAt: DateTime.UtcNow.AddDays(30));

        _service.InvalidateCache();

        var tier = await _service.GetCurrentTierAsync();

        tier.Should().Be(SubscriptionTier.Premium);
    }

    // --- HasFeatureAccessAsync ---

    [Theory]
    [InlineData(FeatureFlag.UnlimitedShelves)]
    [InlineData(FeatureFlag.AdvancedStatistics)]
    [InlineData(FeatureFlag.ExportFunctions)]
    [InlineData(FeatureFlag.CustomThemes)]
    [InlineData(FeatureFlag.AiRecommendations)]
    [InlineData(FeatureFlag.AdFree)]
    public async Task HasFeatureAccessAsync_FreeTier_ShouldDenyPremiumFeatures(FeatureFlag flag)
    {
        var hasAccess = await _service.HasFeatureAccessAsync(flag);

        hasAccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(FeatureFlag.UnlimitedShelves)]
    [InlineData(FeatureFlag.AdvancedStatistics)]
    [InlineData(FeatureFlag.ExportFunctions)]
    [InlineData(FeatureFlag.CustomThemes)]
    [InlineData(FeatureFlag.AiRecommendations)]
    [InlineData(FeatureFlag.AdFree)]
    public async Task HasFeatureAccessAsync_PremiumTier_ShouldGrantAllFeatures(FeatureFlag flag)
    {
        await _service.UpdateTierAsync(SubscriptionTier.Premium);

        var hasAccess = await _service.HasFeatureAccessAsync(flag);

        hasAccess.Should().BeTrue();
    }

    // --- UpdateTierAsync ---

    [Fact]
    public async Task UpdateTierAsync_ShouldPersistAcrossServiceInstances()
    {
        await _service.UpdateTierAsync(SubscriptionTier.Premium, productId: "premium_monthly");

        var newService = new SubscriptionService(_contextFactory);
        var tier = await newService.GetCurrentTierAsync();

        tier.Should().Be(SubscriptionTier.Premium);
    }

    [Fact]
    public async Task UpdateTierAsync_ShouldRaiseTierChangedEvent()
    {
        bool eventRaised = false;
        _service.TierChanged += (_, _) => eventRaised = true;

        await _service.UpdateTierAsync(SubscriptionTier.Premium);

        eventRaised.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTierAsync_SameTier_ShouldNotRaiseEvent()
    {
        // Force record creation with Free tier
        await _service.GetCurrentTierAsync();

        bool eventRaised = false;
        _service.TierChanged += (_, _) => eventRaised = true;

        await _service.UpdateTierAsync(SubscriptionTier.Free);

        eventRaised.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTierAsync_ToPremium_ShouldSetPurchasedAt()
    {
        await _service.UpdateTierAsync(SubscriptionTier.Premium);

        using var context = _contextFactory.CreateDbContext();
        var info = await context.SubscriptionInfos.FirstAsync();

        info.PurchasedAt.Should().NotBeNull();
        info.PurchasedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateTierAsync_ToFree_ShouldClearPurchasedAt()
    {
        await _service.UpdateTierAsync(SubscriptionTier.Premium);
        await _service.UpdateTierAsync(SubscriptionTier.Free);

        using var context = _contextFactory.CreateDbContext();
        var info = await context.SubscriptionInfos.FirstAsync();

        info.PurchasedAt.Should().BeNull();
    }

    // --- Cache ---

    [Fact]
    public async Task InvalidateCache_ShouldForceReloadFromDatabase()
    {
        // Load into cache
        await _service.GetCurrentTierAsync();

        // Directly update database to simulate external change
        using (var ctx = _contextFactory.CreateDbContext())
        {
            var info = await ctx.SubscriptionInfos.FirstAsync();
            info.Tier = SubscriptionTier.Premium;
            info.ExpiresAt = DateTime.UtcNow.AddDays(30);
            await ctx.SaveChangesAsync();
        }

        _service.InvalidateCache();

        var tier = await _service.GetCurrentTierAsync();

        tier.Should().Be(SubscriptionTier.Premium);
    }

    // --- RestorePurchasesAsync ---

    [Fact]
    public async Task RestorePurchasesAsync_ShouldNotThrow()
    {
        var act = () => _service.RestorePurchasesAsync();

        await act.Should().NotThrowAsync();
    }
}
