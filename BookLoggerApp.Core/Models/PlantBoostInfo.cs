namespace BookLoggerApp.Core.Models;

/// <summary>
/// Information about a plant's XP boost contribution.
/// </summary>
public class PlantBoostInfo
{
    public Guid PlantId { get; set; }
    public string PlantName { get; set; } = string.Empty;
    public int PlantLevel { get; set; }
    public decimal BoostPercentage { get; set; } // e.g., 0.08 = 8%
    public string BoostDisplay => $"+{(BoostPercentage * 100):F0}%";
}
