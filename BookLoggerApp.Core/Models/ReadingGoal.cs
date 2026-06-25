using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Core.Models;

/// <summary>A user reading goal (e.g., read 5 books this month).</summary>
public class ReadingGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public GoalType Type { get; set; } // Books, Pages, Minutes

    // Upper bound must match ReadingGoalValidator (<= 100,000) and the Validator_Goal_TargetMax message.
    [Range(1, 100000)]
    public int Target { get; set; } // Target value (e.g., 5 books, 1000 pages, 600 minutes).

    public int Current { get; set; } = 0;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }

    public ICollection<GoalExcludedBook> ExcludedBooks { get; set; } = new List<GoalExcludedBook>();
    public ICollection<GoalGenre> GoalGenres { get; set; } = new List<GoalGenre>();

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public int ProgressPercentage => Target > 0 ? (Current * 100 / Target) : 0;

    // Use local midnight (DateTime.Now), not UtcNow: EndDate comes from the date picker as
    // Kind=Unspecified ticks for the user's local calendar date. Logic lives in GoalActivityHelper
    // so app, repository query, and widget can't drift.
    public bool IsActive => GoalActivityHelper.IsActiveAsOf(this, DateTime.Now);
}
