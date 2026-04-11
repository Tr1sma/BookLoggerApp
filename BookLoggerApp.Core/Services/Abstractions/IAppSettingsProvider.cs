using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Provider for accessing app settings.
/// </summary>
public interface IAppSettingsProvider
{
    /// <summary>
    /// Event raised when user progression data (XP, level, coins) changes.
    /// </summary>
    event EventHandler? ProgressionChanged;

    /// <summary>
    /// Event raised when settings are updated.
    /// </summary>
    event EventHandler? SettingsChanged;

    Task<AppSettings> GetSettingsAsync(CancellationToken ct = default);
    Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default);
    Task<int> GetUserCoinsAsync(CancellationToken ct = default);
    Task<int> GetUserLevelAsync(CancellationToken ct = default);
    Task SpendCoinsAsync(int amount, CancellationToken ct = default);
    Task AddCoinsAsync(int amount, CancellationToken ct = default);
    Task IncrementPlantsPurchasedAsync(CancellationToken ct = default);
    Task<int> GetPlantsPurchasedAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidates the cached settings, forcing a fresh load from the database on next access.
    /// </summary>
    void InvalidateCache();

    /// <summary>
    /// Invalidates the cached settings. When <paramref name="notifyProgressionChanged"/> is false,
    /// subscribers of <see cref="ProgressionChanged"/> are NOT notified — use this during restore
    /// flows where firing the event would trigger subscribers to access stale DbContexts.
    /// </summary>
    void InvalidateCache(bool notifyProgressionChanged);
}
