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

    // For Plants: Reference to PlantSpecies
    public Guid? PlantSpeciesId { get; set; }
    public PlantSpecies? PlantSpecies { get; set; }

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
