namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Reads and persists the UI language. The current value is mirrored into a
/// fast synchronous store (e.g. MAUI <c>Preferences</c>) so that
/// <c>MauiProgram</c> can initialize <c>CultureInfo</c> before the DB is ready,
/// and also persisted into <see cref="BookLoggerApp.Core.Models.AppSettings"/>
/// so it survives a backup round-trip.
/// </summary>
public interface ILanguageService
{
    string CurrentLanguage { get; }

    IReadOnlyList<SupportedLanguage> SupportedLanguages { get; }

    Task SetLanguageAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Called after a backup restore to re-sync the fast synchronous store
    /// (Preferences) from the restored <see cref="BookLoggerApp.Core.Models.AppSettings"/>.
    /// </summary>
    Task SyncFromSettingsAsync(CancellationToken ct = default);
}

public readonly record struct SupportedLanguage(string Code, string DisplayName);
