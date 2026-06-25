using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Display metadata for session moods (emoji, localized label, chart color). Mirrors <see cref="RatingCategoryInfo"/>.
/// </summary>
public class SessionMoodInfo
{
    public SessionMood Mood { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>Chart series theme color for this mood (CLAUDE.md palette).</summary>
    public string ColorHex { get; set; } = string.Empty;

    /// <summary>Returns all moods with their metadata.</summary>
    public static List<SessionMoodInfo> GetAll(IStringLocalizer<AppResources>? localizer = null) => new()
    {
        Create(SessionMood.Crying,      "😭", "Crying",      "#7B8FA3", localizer), // status-planned
        Create(SessionMood.Butterflies, "🦋", "Butterflies", "#C9A97F", localizer), // accent
        Create(SessionMood.Spice,       "🌶️", "Spice",       "#A67874", localizer), // status-abandoned
        Create(SessionMood.Anger,       "😡", "Anger",       "#D4A574", localizer), // primary
        Create(SessionMood.Laughing,    "😂", "Laughing",    "#88A67E", localizer), // status-completed
        Create(SessionMood.MindBlown,   "🤯", "Mind-blown",  "#8B7355", localizer), // secondary
    };

    /// <summary>Returns metadata for a specific mood.</summary>
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
