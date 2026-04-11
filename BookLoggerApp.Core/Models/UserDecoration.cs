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

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
