using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a plant species available in the shop.
/// </summary>
public class PlantSpecies
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    // Growth Mechanics
    [Range(1, 100)]
    public int MaxLevel { get; set; } = 10;

    [Range(1, 365)]
    public int WaterIntervalDays { get; set; } = 3; // Needs watering every X days

    [Range(0.1, 10.0)]
    public double GrowthRate { get; set; } = 1.0; // XP multiplier for leveling

    // XP Boost System
    [Range(0.0, 1.0)]
    public decimal XpBoostPercentage { get; set; } = 0.05m; // Base XP boost (e.g., 0.05 = 5%)

    // Shop
    [Range(0, 1000000)]
    public int BaseCost { get; set; } = 100; // Cost in coins (or XP)

    public bool IsAvailable { get; set; } = true;

    [Range(1, 100)]
    public int UnlockLevel { get; set; } = 1; // User must be level X to unlock

    // Navigation Properties
    public ICollection<UserPlant> UserPlants { get; set; } = new List<UserPlant>();
}
