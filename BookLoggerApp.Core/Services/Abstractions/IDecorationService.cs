using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing cosmetic bookshelf decorations.
/// </summary>
public interface IDecorationService
{
    /// <summary>
    /// Returns all available decoration shop items, ordered by UnlockLevel then Cost.
    /// </summary>
    Task<IReadOnlyList<ShopItem>> GetAllDecorationShopItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns all decorations owned by the user, with ShopItem loaded.
    /// </summary>
    Task<IReadOnlyList<UserDecoration>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Purchases a decoration. Uses static pricing (ShopItem.Cost).
    /// Throws InsufficientFundsException if not enough coins.
    /// </summary>
    Task<UserDecoration> PurchaseDecorationAsync(Guid shopItemId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a decoration and removes all shelf placements.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
