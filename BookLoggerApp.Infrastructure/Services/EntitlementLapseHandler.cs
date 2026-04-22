using Microsoft.EntityFrameworkCore;
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
    /// Clears every hidden flag. Called after <c>EntitlementService.ApplyPurchaseAsync</c>
    /// or <c>ApplyPromoAsync</c> when the new tier is Plus or higher.
    /// </summary>
    public async Task ClearEntitlementHidesAsync(CancellationToken ct = default)
    {
        await using AppDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        var plants = await context.UserPlants
            .Where(p => p.IsHiddenByEntitlement)
            .ToListAsync(ct);
        foreach (var plant in plants)
        {
            plant.IsHiddenByEntitlement = false;
        }

        var shelves = await context.Shelves
            .Where(s => s.IsHiddenByEntitlement)
            .ToListAsync(ct);
        foreach (var shelf in shelves)
        {
            shelf.IsHiddenByEntitlement = false;
        }

        var decorations = await context.UserDecorations
            .Where(d => d.IsHiddenByEntitlement)
            .ToListAsync(ct);
        foreach (var deco in decorations)
        {
            deco.IsHiddenByEntitlement = false;
        }

        if (plants.Count > 0 || shelves.Count > 0 || decorations.Count > 0)
        {
            await context.SaveChangesAsync(ct);
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
