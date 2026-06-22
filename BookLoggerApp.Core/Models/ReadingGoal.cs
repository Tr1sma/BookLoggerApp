using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Helpers;

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

    // Z.598: upper bound matches ReadingGoalValidator (LessThanOrEqualTo 100_000) and the
    // Validator_Goal_TargetMax message ("…cannot exceed 100,000") — the [Range] was 1_000_000.
    [Range(1, 100000)]
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

    // Active is decided against local midnight (DateTime.Now), never DateTime.UtcNow, because
    // EndDate comes from the UI's <input type="date"> picker as Kind=Unspecified with ticks
    // representing the user's local calendar date. The rule lives in GoalActivityHelper so the
    // app, the repository query and the Android widget cannot drift (CODE_REVIEW INK-06).
    public bool IsActive => GoalActivityHelper.IsActiveAsOf(this, DateTime.Now);
}
