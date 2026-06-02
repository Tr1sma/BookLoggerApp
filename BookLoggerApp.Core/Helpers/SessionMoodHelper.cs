using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Hilfsfunktionen für Sitzungs-Stimmungen: erzwingt die Obergrenze und entfernt Duplikate.
/// </summary>
public static class SessionMoodHelper
{
    public const int MaxMoodsPerSession = 3;

    /// <summary>
    /// Distinct + auf <see cref="MaxMoodsPerSession"/> begrenzt; Reihenfolge bleibt erhalten.
    /// </summary>
    public static IReadOnlyList<SessionMood> Clamp(IEnumerable<SessionMood>? moods)
        => (moods ?? Array.Empty<SessionMood>()).Distinct().Take(MaxMoodsPerSession).ToList();
}
