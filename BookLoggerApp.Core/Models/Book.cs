using SQLite;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Minimal book model for M0 seed.
/// </summary>
public sealed class Book
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public ReadingStatus Status { get; set; } = ReadingStatus.Planned;
}

/// <summary>
/// Reading state for a book.
/// </summary>
public enum ReadingStatus
{
    Planned = 0,
    Reading = 1,
    Completed = 2,
    Abandoned = 3
}
