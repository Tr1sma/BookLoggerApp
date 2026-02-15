namespace BookLoggerApp.Core.Models;

/// <summary>
/// Many-to-many relationship tracking genres assigned to a reading goal.
/// When a goal has genres, only books matching at least one genre count toward progress (OR-logic).
/// </summary>
public class GoalGenre
{
    public Guid ReadingGoalId { get; set; }
    public ReadingGoal ReadingGoal { get; set; } = null!;

    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}
