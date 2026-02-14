using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a user reading goal (e.g., read 5 books this month).
/// </summary>
public class ReadingGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public GoalType Type { get; set; } // Books, Pages, Minutes

    [Range(1, 1000000)]
    public int Target { get; set; } // Target value (e.g., 5 books, 1000 pages, 600 minutes)

    public int Current { get; set; } = 0; // Current progress

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }

    // Navigation Properties
    public ICollection<GoalExcludedBook> ExcludedBooks { get; set; } = new List<GoalExcludedBook>();
    public ICollection<GoalGenre> GoalGenres { get; set; } = new List<GoalGenre>();

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Computed Properties
    public int ProgressPercentage => Target > 0 ? (Current * 100 / Target) : 0;
    public bool IsActive => !IsCompleted && DateTime.UtcNow <= EndDate;
}
