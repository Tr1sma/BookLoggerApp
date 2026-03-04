namespace BookLoggerApp.Core.Models;

/// <summary>
/// Aggregated reading statistics for a single month, used for generating shareable stats cards.
/// </summary>
public class MonthlyReadingStats
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int BooksCompleted { get; set; }
    public int PagesRead { get; set; }
    public int MinutesRead { get; set; }
    public int CurrentStreak { get; set; }
    public double AverageRating { get; set; }
    public string? FavoriteGenre { get; set; }
    public int CurrentLevel { get; set; }
    public int TotalXp { get; set; }

    /// <summary>
    /// German month name for the card title.
    /// </summary>
    public string MonthNameGerman => Month switch
    {
        1 => "Januar",
        2 => "Februar",
        3 => "März",
        4 => "April",
        5 => "Mai",
        6 => "Juni",
        7 => "Juli",
        8 => "August",
        9 => "September",
        10 => "Oktober",
        11 => "November",
        12 => "Dezember",
        _ => "?"
    };
}
