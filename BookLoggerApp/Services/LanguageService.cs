using System.Globalization;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Maui.Storage;

namespace BookLoggerApp.Services;

/// <summary>
/// Persists the UI language in Preferences (read synchronously before DB is ready)
/// and in AppSettings.Language (canonical copy that travels with backups).
/// </summary>
public sealed class LanguageService : ILanguageService
{
    public const string PrefKey = "app_language";

    private static readonly SupportedLanguage[] _supported =
    {
        new("en", "English"),
        new("de", "Deutsch"),
    };

    private readonly IAppSettingsProvider _settingsProvider;

    public LanguageService(IAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public IReadOnlyList<SupportedLanguage> SupportedLanguages => _supported;

    public string CurrentLanguage
    {
        get
        {
            string value = Preferences.Default.Get(PrefKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? "en" : value;
        }
    }

    public async Task SetLanguageAsync(string code, CancellationToken ct = default)
    {
        string normalized = NormalizeOrDefault(code);
        Preferences.Default.Set(PrefKey, normalized);

        var settings = await _settingsProvider.GetSettingsAsync(ct);
        if (!string.Equals(settings.Language, normalized, StringComparison.OrdinalIgnoreCase))
        {
            settings.Language = normalized;
            await _settingsProvider.UpdateSettingsAsync(settings, ct);
        }
    }

    public async Task SyncFromSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _settingsProvider.GetSettingsAsync(ct);
        string normalized = NormalizeOrDefault(settings.Language);
        Preferences.Default.Set(PrefKey, normalized);
    }

    private static string NormalizeOrDefault(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "en";
        }
        string lower = code.Trim().ToLowerInvariant();
        foreach (var lang in _supported)
        {
            if (lang.Code == lower)
            {
                return lower;
            }
        }
        return "en";
    }
}
