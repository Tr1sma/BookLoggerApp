using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Eine Stimmungs-Markierung (1 von max. 3) für eine <see cref="ReadingSession"/>.
/// Composite-PK (ReadingSessionId, Mood) verhindert Duplikate pro Sitzung.
/// </summary>
public class ReadingSessionMood
{
    public Guid ReadingSessionId { get; set; }
    public ReadingSession ReadingSession { get; set; } = null!;
    public SessionMood Mood { get; set; }
}
