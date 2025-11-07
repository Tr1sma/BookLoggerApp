using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents user app settings (single-row table).
/// </summary>
public class AppSettings
{
    public Guid Id { get; set; }

    // UI Settings
    [MaxLength(50)]
    public string Theme { get; set; } = "Light"; // Light, Dark, Auto

    [MaxLength(10)]
    public string Language { get; set; } = "en"; // ISO 639-1 code

    // Notifications
    public bool NotificationsEnabled { get; set; } = false;
    public bool ReadingRemindersEnabled { get; set; } = false;
    public TimeSpan? ReminderTime { get; set; } // e.g., 20:00 daily

    // Backup
    public bool AutoBackupEnabled { get; set; } = false;
    public DateTime? LastBackupDate { get; set; }

    // Privacy
    public bool TelemetryEnabled { get; set; } = false;

    // Gamification
    public int UserLevel { get; set; } = 1;
    public int TotalXp { get; set; } = 0;
    public int Coins { get; set; } = 0; // Currency for shop
    public int PlantsPurchased { get; set; } = 0; // Counter for dynamic plant pricing

    // Misc
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
