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
    /// Total experience points accumulated.
    /// </summary>
    public int Experience { get; set; } = 0;

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

    /// <summary>
    /// Optional: Position in bookshelf (for decorative placement).
    /// Format: "shelf-index:slot-index" (e.g., "0:3" = first shelf, 4th slot)
    /// Null if not placed in bookshelf.
    /// </summary>
    [MaxLength(20)]
    public string? BookshelfPosition { get; set; }

    /// <summary>
    /// Whether the plant is displayed in the bookshelf.
    /// </summary>
    public bool IsInBookshelf { get; set; } = false;
}

