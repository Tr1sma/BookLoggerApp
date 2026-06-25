namespace BookLoggerApp.Core.Models;

/// <summary>DTO for generating a book recommendation share card.</summary>
public class BookShareData
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int? PageCount { get; set; }
    public int TotalMinutesRead { get; set; }
    public double? AverageRating { get; set; }
    public Dictionary<RatingCategory, int?> CategoryRatings { get; set; } = new();
    public byte[]? CoverImageBytes { get; set; }
}
