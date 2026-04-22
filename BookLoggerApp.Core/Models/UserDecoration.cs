using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a cosmetic decoration item owned by the user.
/// Purely visual — no growth, no watering, no XP boost.
/// </summary>
public class UserDecoration
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The ShopItem this decoration was purchased from (template reference).
    /// </summary>
    public Guid ShopItemId { get; set; }
    public ShopItem ShopItem { get; set; } = null!;

    /// <summary>
    /// Display name — defaults to ShopItem.Name at purchase time.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Date of purchase (UTC).
    /// </summary>
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True when the user's tier no longer entitles them to this decoration (e.g.
    /// the Heart of Stories ultimate decoration held by a user who lapsed to Free).
    /// The row is preserved so that re-upgrading restores full access.
    /// </summary>
    public bool IsHiddenByEntitlement { get; set; } = false;

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
