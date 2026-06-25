namespace BookLoggerApp.Core.Enums;

/// <summary>
/// Emotional reaction tagged to a reading session. Persisted as int via the
/// ReadingSessionMood child table.
/// </summary>
public enum SessionMood
{
    Crying = 0,       // 😭
    Butterflies = 1,  // 🦋
    Spice = 2,        // 🌶️
    Anger = 3,        // 😡
    Laughing = 4,     // 😂
    MindBlown = 5     // 🤯
}
