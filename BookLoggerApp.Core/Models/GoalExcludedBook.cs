namespace BookLoggerApp.Core.Models;

/// <summary>
/// Books excluded from a reading goal; excluded books and their sessions don't count toward progress.
/// </summary>
public class GoalExcludedBook
{
    public Guid ReadingGoalId { get; set; }
    public ReadingGoal ReadingGoal { get; set; } = null!;

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
}
