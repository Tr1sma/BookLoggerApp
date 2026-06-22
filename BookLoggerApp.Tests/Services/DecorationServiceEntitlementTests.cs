using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// CODE_REVIEW SEC-06 (decoration-tier purchase entitlement enforced in the service) and
/// SEC-11 (hidden-by-entitlement decorations must not surface in reads nor grant their
/// special-ability boost after a downgrade).
/// </summary>
public class DecorationServiceEntitlementTests : IDisposable
{
    private readonly DbContextTestHelper _dbHelper;
    private readonly TestDbContextFactory _factory;
    private readonly AppSettingsProvider _settingsProvider;

    public DecorationServiceEntitlementTests()
    {
        _dbHelper = DbContextTestHelper.CreateTestContext();
        _factory = new TestDbContextFactory(_dbHelper.DatabaseName);
        _settingsProvider = new AppSettingsProvider(_factory);
    }

    public void Dispose() => _dbHelper.Dispose();

    private DecorationService CreateService(SubscriptionTier tier) => new(
        _factory,
        _settingsProvider,
        NullLogger<DecorationService>.Instance,
        featureGuard: new FeatureGuard(new FakeEntitlementService(tier)));

    private async Task<ShopItem> SeedDecorationAsync(bool isFree, bool isUltimate, string? abilityKey = null)
    {
        var item = new ShopItem
        {
            Id = Guid.NewGuid(),
            ItemType = ShopItemType.Decoration,
            Name = "Tier Deco",
            Cost = 100,
            ImagePath = "/x.svg",
            IsAvailable = true,
            UnlockLevel = 1,
            IsFreeTier = isFree,
            IsUltimateTier = isUltimate,
            SpecialAbilityKey = abilityKey
        };
        _dbHelper.Context.ShopItems.Add(item);
        await _dbHelper.Context.SaveChangesAsync();
        return item;
    }

    private async Task GiveCoinsAsync(int amount)
    {
        var settings = await _dbHelper.Context.AppSettings.FirstAsync();
        settings.Coins = amount;
        await _dbHelper.Context.SaveChangesAsync();
        _settingsProvider.InvalidateCache();
    }

    [Fact]
    public async Task PurchaseDecorationAsync_FreeUser_StandardDecoration_ThrowsAndDoesNotSpendCoins()
    {
        var item = await SeedDecorationAsync(isFree: false, isUltimate: false);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.PurchaseDecorationAsync(item.Id);

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.StandardPlantsAndDecorations);

        (await _settingsProvider.GetUserCoinsAsync()).Should().Be(10_000);
    }

    [Fact]
    public async Task PurchaseDecorationAsync_PlusUser_UltimateDecoration_ThrowsAndDoesNotSpendCoins()
    {
        var item = await SeedDecorationAsync(isFree: false, isUltimate: true);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Plus);

        Func<Task> act = () => service.PurchaseDecorationAsync(item.Id);

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.UltimateDecorations);

        (await _settingsProvider.GetUserCoinsAsync()).Should().Be(10_000);
    }

    [Fact]
    public async Task PurchaseDecorationAsync_PremiumUser_UltimateDecoration_Succeeds()
    {
        var item = await SeedDecorationAsync(isFree: false, isUltimate: true);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Premium);

        var deco = await service.PurchaseDecorationAsync(item.Id);

        deco.Should().NotBeNull();
        deco.ShopItemId.Should().Be(item.Id);
    }

    [Fact]
    public async Task PurchaseDecorationAsync_FreeUser_FreeTierDecoration_Succeeds()
    {
        var item = await SeedDecorationAsync(isFree: true, isUltimate: false);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Free);

        var deco = await service.PurchaseDecorationAsync(item.Id);

        deco.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ExcludesDecorationsHiddenByEntitlement()
    {
        var item = await SeedDecorationAsync(isFree: false, isUltimate: true);
        _dbHelper.Context.UserDecorations.Add(new UserDecoration
        {
            Id = Guid.NewGuid(), ShopItemId = item.Id, Name = "Visible", PurchasedAt = DateTime.UtcNow,
            IsHiddenByEntitlement = false
        });
        _dbHelper.Context.UserDecorations.Add(new UserDecoration
        {
            Id = Guid.NewGuid(), ShopItemId = item.Id, Name = "HiddenUltimate", PurchasedAt = DateTime.UtcNow,
            IsHiddenByEntitlement = true
        });
        await _dbHelper.Context.SaveChangesAsync();

        var service = CreateService(SubscriptionTier.Free);
        var decorations = await service.GetAllAsync();

        decorations.Should().ContainSingle().Which.Name.Should().Be("Visible");
    }

    [Fact]
    public async Task UserOwnsAbilityAsync_IgnoresDecorationHiddenByEntitlement()
    {
        var item = await SeedDecorationAsync(isFree: false, isUltimate: true, abilityKey: SpecialAbilityKeys.StoryHeart);
        _dbHelper.Context.UserDecorations.Add(new UserDecoration
        {
            Id = Guid.NewGuid(), ShopItemId = item.Id, Name = "HiddenHeart", PurchasedAt = DateTime.UtcNow,
            IsHiddenByEntitlement = true
        });
        await _dbHelper.Context.SaveChangesAsync();

        var service = CreateService(SubscriptionTier.Free);

        (await service.UserOwnsAbilityAsync(SpecialAbilityKeys.StoryHeart))
            .Should().BeFalse("a hidden ultimate decoration must not grant its gameplay boost after a downgrade");
    }
}
