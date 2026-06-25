using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// A mood tag (1 of max 3) for a <see cref="ReadingSession"/>. Composite PK
/// (ReadingSessionId, Mood) prevents duplicates per session.
/// </summary>
public class ReadingSessionMood
{
    public Guid ReadingSessionId { get; set; }
    public ReadingSession ReadingSession { get; set; } = null!;
    public SessionMood Mood { get; set; }
}
