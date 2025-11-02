using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a plant owned by the user.
/// </summary>
public class UserPlant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid SpeciesId { get; set; }
    public PlantSpecies Species { get; set; } = null!;

    // User-specific data
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // User-given name

    [Range(1, 100)]
    public int CurrentLevel { get; set; } = 1;

    [Range(0, 1000000)]
    public int Experience { get; set; } = 0;

    public PlantStatus Status { get; set; } = PlantStatus.Healthy;

    public DateTime LastWatered { get; set; } = DateTime.UtcNow;
    public DateTime PlantedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true; // Currently displayed plant
}
