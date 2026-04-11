using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Service implementation for cosmetic bookshelf decorations.
/// Uses IDbContextFactory (same pattern as ShelfService).
/// </summary>
public class DecorationService : IDecorationService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly ILogger<DecorationService> _logger;

    public DecorationService(
        IDbContextFactory<AppDbContext> contextFactory,
        IAppSettingsProvider settingsProvider,
        ILogger<DecorationService> logger)
    {
        _contextFactory = contextFactory;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ShopItem>> GetAllDecorationShopItemsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.ShopItems
            .Where(si => si.ItemType == ShopItemType.Decoration && si.IsAvailable)
            .OrderBy(si => si.UnlockLevel)
            .ThenBy(si => si.Cost)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserDecoration>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.UserDecorations
            .Include(d => d.ShopItem)
            .ToListAsync(ct);
    }

    public async Task<UserDecoration> PurchaseDecorationAsync(Guid shopItemId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var shopItem = await context.ShopItems.FindAsync(new object[] { shopItemId }, ct);
        if (shopItem == null)
            throw new EntityNotFoundException(typeof(ShopItem), shopItemId);

        if (shopItem.ItemType != ShopItemType.Decoration)
            throw new InvalidOperationException("ShopItem is not a decoration.");

        if (!shopItem.IsAvailable)
            throw new InvalidOperationException("This decoration is not available for purchase.");

        // Static pricing — use ShopItem.Cost directly (no dynamic multiplier)
        await _settingsProvider.SpendCoinsAsync(shopItem.Cost, ct);

        var decoration = new UserDecoration
        {
            Id = Guid.NewGuid(),
            ShopItemId = shopItemId,
            Name = shopItem.Name,
            PurchasedAt = DateTime.UtcNow
        };

        context.UserDecorations.Add(decoration);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("Purchased decoration '{Name}' for {Cost} coins", shopItem.Name, shopItem.Cost);
        return decoration;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var decoration = await context.UserDecorations.FindAsync(new object[] { id }, ct);
        if (decoration == null) return;

        // Remove shelf placement entries first
        var placements = await context.DecorationShelves
            .Where(ds => ds.DecorationId == id)
            .ToListAsync(ct);
        context.DecorationShelves.RemoveRange(placements);

        context.UserDecorations.Remove(decoration);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted decoration '{Name}' (ID: {Id})", decoration.Name, id);
    }
}
