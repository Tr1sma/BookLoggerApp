using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Runs the data-guard when a user lapses to Free or re-upgrades to a paid tier.
///
/// <para><b>Lapse flow</b> (Plus/Premium → Free): hides overflow data without
/// deleting it — exactly one plant stays active (deterministic tie-break), shelves
/// beyond the 3-shelf Free cap get <c>IsHiddenByEntitlement</c>, Prestige plants
/// and the Heart of Stories decoration also get hidden. Everything can be restored
/// on re-upgrade.</para>
///
/// <para><b>Restore flow</b> (Free → Plus/Premium): clears every
/// <c>IsHiddenByEntitlement</c> flag so the user sees their data again.</para>
/// </summary>
public class EntitlementLapseHandler
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public EntitlementLapseHandler(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Applies the Free-tier visibility rules. Called from
    /// <c>EntitlementService.ApplyLapseAsync</c> after the tier has flipped.
    /// </summary>
    public async Task ApplyLapseAsync(CancellationToken ct = default)
    {
        await using AppDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        await HideOverflowPlantsAsync(context, ct);
        await HideOverflowShelvesAsync(context, ct);
        await HideUltimateDecorationsAsync(context, ct);

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Clears the hidden flags the new <paramref name="tier"/> is entitled to. Called after
    /// <c>EntitlementService.ApplyPurchaseAsync</c> or <c>ApplyPromoAsync</c> when the new
    /// tier is Plus or higher.
    ///
    /// <para><b>Tier-aware (CODE_REVIEW SEC-04):</b> Premium clears everything. Plus only
    /// restores content it is entitled to (shelves, standard plants/decorations) and
    /// <em>keeps Premium-only content hidden</em> — prestige plants and ultimate decorations.
    /// On a Premium→Plus downgrade this also re-hides those items if they were still visible.</para>
    /// </summary>
    public async Task ClearEntitlementHidesAsync(SubscriptionTier tier, CancellationToken ct = default)
    {
        await using AppDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        bool premium = tier >= SubscriptionTier.Premium;

        // Shelves: both Plus and Premium are entitled to unlimited shelves.
        var shelves = await context.Shelves
            .Where(s => s.IsHiddenByEntitlement)
            .ToListAsync(ct);
        foreach (var shelf in shelves)
        {
            shelf.IsHiddenByEntitlement = false;
        }

        // Plants: standard plants unlock at Plus; prestige plants require Premium. For Plus
        // we keep/force prestige plants hidden (and inactive); for Premium everything is freed.
        var plants = await context.UserPlants
            .Include(p => p.Species)
            .ToListAsync(ct);
        foreach (var plant in plants)
        {
            if (plant.Species.IsPrestigeTier && !premium)
            {
                plant.IsHiddenByEntitlement = true;
                plant.IsActive = false;
            }
            else if (plant.IsHiddenByEntitlement)
            {
                plant.IsHiddenByEntitlement = false;
            }
        }

        EnsureOneActivePlant(plants);

        // Decorations: standard decorations unlock at Plus; ultimate decorations require Premium.
        var decorations = await context.UserDecorations
            .Include(d => d.ShopItem)
            .ToListAsync(ct);
        foreach (var deco in decorations)
        {
            if (deco.ShopItem.IsUltimateTier && !premium)
            {
                deco.IsHiddenByEntitlement = true;
            }
            else if (deco.IsHiddenByEntitlement)
            {
                deco.IsHiddenByEntitlement = false;
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Guarantees at least one visible plant is active. Only promotes a fallback when
    /// re-hiding (e.g. a Premium→Plus downgrade that hid the active prestige plant) left
    /// the user with no active plant at all.
    /// </summary>
    private static void EnsureOneActivePlant(List<UserPlant> plants)
    {
        if (plants.Count == 0)
        {
            return;
        }

        if (plants.Any(p => p.IsActive && !p.IsHiddenByEntitlement))
        {
            return;
        }

        UserPlant? fallback = plants
            .Where(p => !p.IsHiddenByEntitlement && !p.Species.IsPrestigeTier)
            .OrderBy(p => p.PlantedAt)
            .FirstOrDefault();
        if (fallback is not null)
        {
            fallback.IsActive = true;
        }
    }

    private static async Task HideOverflowPlantsAsync(AppDbContext context, CancellationToken ct)
    {
        var plants = await context.UserPlants
            .Include(p => p.Species)
            .ToListAsync(ct);

        if (plants.Count == 0)
        {
            return;
        }

        // Determine the one plant that stays active: prefer currently-active healthy
        // plants, then fall back to the oldest plant by PlantedAt.
        UserPlant? keepActive = plants
            .Where(p => p.IsActive && p.Status == PlantStatus.Healthy)
            .OrderBy(p => p.PlantedAt)
            .FirstOrDefault();

        keepActive ??= plants
            .Where(p => p.IsActive)
            .OrderBy(p => p.PlantedAt)
            .FirstOrDefault();

        keepActive ??= plants.OrderBy(p => p.PlantedAt).First();

        foreach (var plant in plants)
        {
            plant.IsActive = plant.Id == keepActive.Id;

            // Prestige plants are entirely hidden on Free — even the one that would
            // otherwise stay active. In that edge case we fall back to the oldest
            // non-prestige plant.
            if (plant.Species.IsPrestigeTier)
            {
                plant.IsHiddenByEntitlement = true;
                plant.IsActive = false;
            }
        }

        // Second pass: make sure there is still exactly one active plant after the
        // prestige-hide step (possible edge case when keepActive itself was prestige).
        bool hasAnyActive = plants.Any(p => p.IsActive);
        if (!hasAnyActive)
        {
            var fallback = plants
                .Where(p => !p.Species.IsPrestigeTier)
                .OrderBy(p => p.PlantedAt)
                .FirstOrDefault();
            if (fallback is not null)
            {
                fallback.IsActive = true;
            }
        }
    }

    private static async Task HideOverflowShelvesAsync(AppDbContext context, CancellationToken ct)
    {
        const int freeShelfCap = 3;

        var shelves = await context.Shelves
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        for (int i = 0; i < shelves.Count; i++)
        {
            shelves[i].IsHiddenByEntitlement = i >= freeShelfCap;
        }
    }

    private static async Task HideUltimateDecorationsAsync(AppDbContext context, CancellationToken ct)
    {
        var decorations = await context.UserDecorations
            .Include(d => d.ShopItem)
            .ToListAsync(ct);

        foreach (var deco in decorations)
        {
            deco.IsHiddenByEntitlement = deco.ShopItem.IsUltimateTier;
        }
    }
}
