namespace BookLoggerApp.Core.Models;

/// <summary>
/// Many-to-many relationship tracking books excluded from a reading goal.
/// Excluded books and their reading sessions do not count toward the goal's progress.
/// </summary>
public class GoalExcludedBook
{
    public Guid ReadingGoalId { get; set; }
    public ReadingGoal ReadingGoal { get; set; } = null!;

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
}
