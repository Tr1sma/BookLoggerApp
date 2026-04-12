namespace BookLoggerApp.Core.Models;

public record AuthorStats(
    string Author,
    int BookCount,
    int TotalPages);
