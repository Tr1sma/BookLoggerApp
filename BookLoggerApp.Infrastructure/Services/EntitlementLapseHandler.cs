using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Hides overflow data on Free lapse (plants, shelves, ultimate decorations) and
/// restores all hidden data on re-upgrade. Nothing is deleted — only <c>IsHiddenByEntitlement</c>.
/// </summary>
public class EntitlementLapseHandler
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public EntitlementLapseHandler(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task ApplyLapseAsync(CancellationToken ct = default)
    {
        await using AppDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        await HideOverflowPlantsAsync(context, ct);
        await HideOverflowShelvesAsync(context, ct);
        await HideUltimateDecorationsAsync(context, ct);

        await context.SaveChangesAsync(ct);
    }

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

        // Prefer active+healthy, then oldest.
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

            // Prestige always hidden on Free, even if keepActive.
            if (plant.Species.IsPrestigeTier)
            {
                plant.IsHiddenByEntitlement = true;
                plant.IsActive = false;
            }
        }

        // keepActive may itself have been prestige — ensure one active remains.
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
