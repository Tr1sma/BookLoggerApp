using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Anzeige-Metadaten für die Sitzungs-Stimmungen (Emoji, lokalisiertes Label, Chart-Farbe).
/// Spiegelt das Muster von <see cref="RatingCategoryInfo"/>.
/// </summary>
public class SessionMoodInfo
{
    public SessionMood Mood { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>Theme-Farbe der Chart-Serie für diese Stimmung (CLAUDE.md-Palette).</summary>
    public string ColorHex { get; set; } = string.Empty;

    /// <summary>
    /// Liefert alle Stimmungen mit ihren Metadaten.
    /// </summary>
    public static List<SessionMoodInfo> GetAll(IStringLocalizer<AppResources>? localizer = null) => new()
    {
        Create(SessionMood.Crying,      "😭", "Crying",      "#7B8FA3", localizer), // status-planned (blau-grau)
        Create(SessionMood.Butterflies, "🦋", "Butterflies", "#C9A97F", localizer), // accent (helles Braun)
        Create(SessionMood.Spice,       "🌶️", "Spice",       "#A67874", localizer), // status-abandoned (rot)
        Create(SessionMood.Anger,       "😡", "Anger",       "#D4A574", localizer), // primary (Beige)
        Create(SessionMood.Laughing,    "😂", "Laughing",    "#88A67E", localizer), // status-completed (grün)
        Create(SessionMood.MindBlown,   "🤯", "Mind-blown",  "#8B7355", localizer), // secondary (gedämpftes Braun)
    };

    /// <summary>
    /// Liefert die Metadaten zu einer bestimmten Stimmung.
    /// </summary>
    public static SessionMoodInfo? Get(SessionMood mood, IStringLocalizer<AppResources>? localizer = null)
        => GetAll(localizer).FirstOrDefault(m => m.Mood == mood);

    private static SessionMoodInfo Create(
        SessionMood mood,
        string emoji,
        string fallbackLabel,
        string colorHex,
        IStringLocalizer<AppResources>? localizer) => new()
    {
        Mood = mood,
        Emoji = emoji,
        ColorHex = colorHex,
        Label = GetLocalized(localizer, $"Session_Mood_{mood}_Label", fallbackLabel)
    };

    private static string GetLocalized(IStringLocalizer<AppResources>? localizer, string key, string fallback)
    {
        if (localizer is null)
        {
            return fallback;
        }

        var localized = localizer[key];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }
}
