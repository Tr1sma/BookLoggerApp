using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Single source of truth mapping a shop item's tier flags to the <see cref="FeatureKey"/>
/// that gates its purchase. Shared by the shop UI (PlantShop.razor) and the service-layer
/// entitlement guards (<c>PlantService.PurchasePlantAsync</c> /
/// <c>DecorationService.PurchaseDecorationAsync</c>) so the gate can never diverge between
/// the UI overlay and the load-bearing server-side check (CODE_REVIEW SEC-08 / SEC-06).
///
/// <para>A <c>null</c> result means the item is Free-tier and requires no entitlement.</para>
/// </summary>
public static class ShopTierFeatures
{
    /// <summary>Returns the <see cref="FeatureKey"/> required to purchase <paramref name="species"/>, or null for Free-tier plants.</summary>
    public static FeatureKey? For(PlantSpecies species)
    {
        if (species.IsPrestigeTier)
        {
            return FeatureKey.PrestigePlants;
        }
        if (species.IsFreeTier)
        {
            return null;
        }
        return FeatureKey.StandardPlantsAndDecorations;
    }

    /// <summary>Returns the <see cref="FeatureKey"/> required to purchase <paramref name="item"/>, or null for Free-tier decorations.</summary>
    public static FeatureKey? For(ShopItem item)
    {
        if (item.IsUltimateTier)
        {
            return FeatureKey.UltimateDecorations;
        }
        if (item.IsFreeTier)
        {
            return null;
        }
        return FeatureKey.StandardPlantsAndDecorations;
    }
}
