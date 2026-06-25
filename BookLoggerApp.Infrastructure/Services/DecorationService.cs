using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>Cosmetic bookshelf decorations service (uses IDbContextFactory).</summary>
public class DecorationService : IDecorationService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly ILogger<DecorationService> _logger;
    private readonly IFeatureGuard? _featureGuard;

    public DecorationService(
        IDbContextFactory<AppDbContext> contextFactory,
        IAppSettingsProvider settingsProvider,
        ILogger<DecorationService> logger,
        IFeatureGuard? featureGuard = null)
    {
        _contextFactory = contextFactory;
        _settingsProvider = settingsProvider;
        _logger = logger;
        _featureGuard = featureGuard;
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
        // Hide entitlement-locked decorations so they neither render nor contribute their boost (SEC-11).
        return await context.UserDecorations
            .Include(d => d.ShopItem)
            .Where(d => !d.IsHiddenByEntitlement)
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

        if (shopItem.IsSingleton)
        {
            bool alreadyOwned = await context.UserDecorations
                .AnyAsync(d => d.ShopItemId == shopItemId, ct);
            if (alreadyOwned)
                throw new InvalidOperationException("Dieses Relikt kannst du nur einmal besitzen.");
        }

        // Enforce tier server-side (SEC-06): the UI LockedFeatureButton is only a hint, so block
        // buying a Plus/Premium decoration without the subscription. Mirror ShopTierFeatures.
        if (_featureGuard is not null)
        {
            FeatureKey? tierFeature = ShopTierFeatures.For(shopItem);
            if (tierFeature is not null)
            {
                _featureGuard.RequireAccess(tierFeature.Value, "This decoration requires a higher subscription tier.");
            }
        }

        await _settingsProvider.SpendCoinsAsync(shopItem.Cost, ct);

        // Settings and decoration use separate DbContexts, so no single transaction covers both:
        // if the save fails after coins are spent, refund explicitly so the user isn't overcharged.
        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decoration purchase save failed after coins were spent. Refunding {Cost} coins for ShopItem {ShopItemId}.", shopItem.Cost, shopItemId);
            try
            {
                await _settingsProvider.AddCoinsAsync(shopItem.Cost, ct);
            }
            catch (Exception refundEx)
            {
                _logger.LogCritical(refundEx, "CRITICAL: Could not refund {Cost} coins after failed decoration purchase for ShopItem {ShopItemId}. User coin balance is incorrect.", shopItem.Cost, shopItemId);
            }
            throw;
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var decoration = await context.UserDecorations.FindAsync(new object[] { id }, ct);
        if (decoration == null) return;

        // Remove shelf placement entries first.
        var placements = await context.DecorationShelves
            .Where(ds => ds.DecorationId == id)
            .ToListAsync(ct);
        context.DecorationShelves.RemoveRange(placements);

        context.UserDecorations.Remove(decoration);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted decoration '{Name}' (ID: {Id})", decoration.Name, id);
    }

    public async Task<bool> UserOwnsAbilityAsync(string abilityKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(abilityKey))
            return false;

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        // A decoration hidden by an entitlement lapse must not grant its special-ability boost (SEC-11).
        return await context.UserDecorations
            .AnyAsync(d => !d.IsHiddenByEntitlement && d.ShopItem != null && d.ShopItem.SpecialAbilityKey == abilityKey, ct);
    }
}
