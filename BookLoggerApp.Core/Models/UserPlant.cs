using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Enums;
namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a plant owned by the user.
/// </summary>
public class UserPlant
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the plant species (type).
    /// </summary>
    public Guid SpeciesId { get; set; }
    public PlantSpecies Species { get; set; } = null!;

    /// <summary>
    /// User-defined name for this plant instance.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current level of the plant.
    /// </summary>
    public int CurrentLevel { get; set; } = 1;

    /// <summary>
    /// Total experience points accumulated (legacy, kept for potential future use).
    /// </summary>
    public int Experience { get; set; } = 0;

    /// <summary>
    /// Number of reading days credited to this plant.
    /// A reading day = day with at least 15 min of reading time (while plant was active).
    /// Used for plant leveling: 3 reading days = 1 level (at GrowthRate 1.0).
    /// </summary>
    public int ReadingDaysCount { get; set; } = 0;

    /// <summary>
    /// Last day a reading day was credited to this plant.
    /// Prevents double-counting on the same day.
    /// </summary>
    public DateTime? LastReadingDayRecorded { get; set; }

    /// <summary>
    /// Current health status (Healthy, Thirsty, Wilting, Dead).
    /// </summary>
    public Enums.PlantStatus Status { get; set; } = Enums.PlantStatus.Healthy;


    /// <summary>
    /// Date and time when the plant was purchased/planted.
    /// </summary>
    public DateTime PlantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the plant was last watered.
    /// </summary>
    public DateTime LastWatered { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this plant is currently active (selected for XP gain).
    /// </summary>
    public bool IsActive { get; set; } = false;

    public ICollection<PlantShelf> PlantShelves { get; set; } = new List<PlantShelf>();

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

