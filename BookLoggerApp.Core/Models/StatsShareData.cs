namespace BookLoggerApp.Core.Models;

/// <summary>
/// Data transfer object for generating a reading stats share card.
/// </summary>
public class StatsShareData
{
    public string PeriodLabel { get; set; } = string.Empty;
    public int BooksCompleted { get; set; }
    public int PagesRead { get; set; }
    public int MinutesRead { get; set; }
    public string? FavoriteGenre { get; set; }
    public List<(string Title, string Author)> TopBooks { get; set; } = new();
    public int UserLevel { get; set; }
}
