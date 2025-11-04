using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Provider for accessing app settings.
/// </summary>
public interface IAppSettingsProvider
{
    Task<AppSettings> GetSettingsAsync(CancellationToken ct = default);
    Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default);
    Task<int> GetUserCoinsAsync(CancellationToken ct = default);
    Task<int> GetUserLevelAsync(CancellationToken ct = default);
    Task SpendCoinsAsync(int amount, CancellationToken ct = default);
    Task AddCoinsAsync(int amount, CancellationToken ct = default);
}
