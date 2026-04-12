namespace BookLoggerApp.Core.Models;

public record YearStats(
    int Year,
    int BooksCompleted,
    int PagesRead,
    int MinutesRead,
    double AverageRating);
