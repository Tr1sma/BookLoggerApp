namespace BookLoggerApp.Core.Models;

/// <summary>
/// A single reading session entry.
/// </summary>
public sealed class ReadingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int Minutes { get; set; } = 0;
    public int? PagesRead { get; set; } // optional for later stats
}
