using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Data-guard for tier changes. Lapse (paid → Free) hides overflow data without deleting it;
/// restore (Free → paid) clears the <c>IsHiddenByEntitlement</c> flags the new tier is entitled to.
/// </summary>
public class EntitlementLapseHandler
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public EntitlementLapseHandler(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Reconciles entitlement-gated content against the current <paramref name="tier"/> (HIGH-1003),
    /// so out-of-band content (backup-restore / JSON-import) can't leak to a lower tier. Idempotent.
    /// </summary>
    public Task ReconcileAsync(SubscriptionTier tier, CancellationToken ct = default)
    {
        return tier <= SubscriptionTier.Free
            ? ApplyLapseAsync(ct)
            : ClearEntitlementHidesAsync(tier, ct);
    }

    /// <summary>Applies Free-tier visibility rules after a lapse.</summary>
    public async Task ApplyLapseAsync(CancellationToken ct = default)
    {
        await using AppDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        await HideOverflowPlantsAsync(context, ct);
        await HideOverflowShelvesAsync(context, ct);
        await HideNonFreeDecorationsAsync(context, ct);

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Clears the hidden flags the new <paramref name="tier"/> is entitled to (SEC-04). Premium clears
    /// everything; Plus keeps Premium-only content (prestige plants, ultimate decorations) hidden.
    /// </summary>
    public async Task ClearEntitlementHidesAsync(SubscriptionTier tier, CancellationToken ct = default)
    {
        await using AppDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        bool premium = tier >= SubscriptionTier.Premium;

        // Both Plus and Premium get unlimited shelves.
        var shelves = await context.Shelves
            .Where(s => s.IsHiddenByEntitlement)
            .ToListAsync(ct);
        foreach (var shelf in shelves)
        {
            shelf.IsHiddenByEntitlement = false;
        }

        // Standard plants unlock at Plus, prestige plants require Premium: Plus keeps prestige hidden/inactive.
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

        // Standard decorations unlock at Plus, ultimate decorations require Premium.
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

    /// <summary>Guarantees at least one visible plant is active, promoting a fallback only if none is.</summary>
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

        // Free keeps only free-tier plants; hide (not delete) standard/prestige so re-upgrade
        // restores them and an imported higher-tier backup can't leak them (HIGH-1003).
        foreach (var plant in plants)
        {
            if (!plant.Species.IsFreeTier)
            {
                plant.IsHiddenByEntitlement = true;
                plant.IsActive = false;
            }
        }

        // Keep exactly one visible plant active: prefer active+healthy, then any active, then oldest.
        var visible = plants.Where(p => !p.IsHiddenByEntitlement).ToList();
        if (visible.Count == 0)
        {
            return;
        }

        UserPlant? keepActive = visible
            .Where(p => p.IsActive && p.Status == PlantStatus.Healthy)
            .OrderBy(p => p.PlantedAt)
            .FirstOrDefault();

        keepActive ??= visible
            .Where(p => p.IsActive)
            .OrderBy(p => p.PlantedAt)
            .FirstOrDefault();

        keepActive ??= visible.OrderBy(p => p.PlantedAt).First();

        foreach (var plant in visible)
        {
            plant.IsActive = plant.Id == keepActive.Id;
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

    private static async Task HideNonFreeDecorationsAsync(AppDbContext context, CancellationToken ct)
    {
        var decorations = await context.UserDecorations
            .Include(d => d.ShopItem)
            .ToListAsync(ct);

        foreach (var deco in decorations)
        {
            // Free keeps only free-tier decorations; hide standard and ultimate (HIGH-1003).
            deco.IsHiddenByEntitlement = !deco.ShopItem.IsFreeTier;
        }
    }
}
