using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a single reading session for a book.
/// </summary>
public class ReadingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!; // Navigation Property

    // Session Data
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    [Range(0, 1440)] // Max 24 hours
    public int Minutes { get; set; } = 0;

    [Range(0, 10000)]
    public int? PagesRead { get; set; }

    public int? StartPage { get; set; }
    public int? EndPage { get; set; }

    // Gamification
    public int XpEarned { get; set; } = 0; // Calculated on save

    // Notes
    [MaxLength(1000)]
    public string? Notes { get; set; }
}
