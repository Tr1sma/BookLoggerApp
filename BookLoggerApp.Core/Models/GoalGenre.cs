namespace BookLoggerApp.Core.Models;

/// <summary>
/// Genres assigned to a reading goal. When set, only books matching at least one genre
/// count toward progress (OR-logic).
/// </summary>
public class GoalGenre
{
    public Guid ReadingGoalId { get; set; }
    public ReadingGoal ReadingGoal { get; set; } = null!;

    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}
