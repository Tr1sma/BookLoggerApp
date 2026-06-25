using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>A single reading session for a book.</summary>
public class ReadingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    [Range(0, 1440)] // Max 24 hours.
    public int Minutes { get; set; } = 0;

    [Range(0, 10000)]
    public int? PagesRead { get; set; }

    public int? StartPage { get; set; }
    public int? EndPage { get; set; }

    public int XpEarned { get; set; } = 0; // Calculated on save.

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Mood/trigger tags (1-3) as child entities; cascade-deleted with the session.
    public ICollection<ReadingSessionMood> Moods { get; set; } = new List<ReadingSessionMood>();

    /// <summary>Convenience access to the tagged moods (not mapped).</summary>
    [NotMapped]
    public IReadOnlyList<SessionMood> MoodList => Moods.Select(m => m.Mood).Distinct().ToList();

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
