using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Session-mood helpers: enforces the per-session cap and removes duplicates.
/// </summary>
public static class SessionMoodHelper
{
    public const int MaxMoodsPerSession = 3;

    /// <summary>
    /// Distinct + capped at <see cref="MaxMoodsPerSession"/>; order is preserved.
    /// </summary>
    public static IReadOnlyList<SessionMood> Clamp(IEnumerable<SessionMood>? moods)
        => (moods ?? Array.Empty<SessionMood>()).Distinct().Take(MaxMoodsPerSession).ToList();
}
