using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents an item available in the plant shop (plants, decorations, themes).
/// </summary>
public class ShopItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ShopItemType ItemType { get; set; } // Plant, Theme, Decoration

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 1000000)]
    public int Cost { get; set; } // Cost in coins or XP

    [MaxLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;

    [Range(1, 100)]
    public int UnlockLevel { get; set; } = 1;

    [Range(1, 4)]
    public int SlotWidth { get; set; } = 1;

    /// <summary>
    /// Optional key identifying a special gameplay ability (e.g. "story_heart").
    /// Null for ordinary cosmetic items. See <see cref="SpecialAbilityKeys"/>.
    /// </summary>
    [MaxLength(50)]
    public string? SpecialAbilityKey { get; set; }

    /// <summary>
    /// If true, the user can own at most one instance of this item.
    /// Purchase attempts while the user already owns it must be rejected.
    /// </summary>
    public bool IsSingleton { get; set; }

    /// <summary>
    /// True if this item is purchasable by Free-tier users (one of the 3 starter decorations).
    /// Plus unlocks everything that is not <see cref="IsUltimateTier"/>.
    /// </summary>
    public bool IsFreeTier { get; set; } = false;

    /// <summary>
    /// True for the Premium-exclusive ultimate decoration (Heart of Stories).
    /// </summary>
    public bool IsUltimateTier { get; set; } = false;

    // For Plants: Reference to PlantSpecies
    public Guid? PlantSpeciesId { get; set; }
    public PlantSpecies? PlantSpecies { get; set; }

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
