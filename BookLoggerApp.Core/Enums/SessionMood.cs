namespace BookLoggerApp.Core.Enums;

/// <summary>
/// Emotionale Reaktion, die einer einzelnen Lesesitzung zugeordnet werden kann.
/// Persistiert als int über die Kind-Tabelle ReadingSessionMood.
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
