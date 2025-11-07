using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Provider implementation for app settings.
/// </summary>
public class AppSettingsProvider : IAppSettingsProvider
{
    private readonly AppDbContext _context;

    public event EventHandler? ProgressionChanged;

    public AppSettingsProvider(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Raises the ProgressionChanged event to notify subscribers of progression data changes.
    /// </summary>
    private void OnProgressionChanged()
    {
        ProgressionChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync(ct);

        if (settings == null)
        {
            // Create default settings if none exist
            settings = new AppSettings
            {
                Theme = "Light",
                Language = "en",
                UserLevel = 1,
                TotalXp = 0,
                Coins = 100 // Start with 100 coins
            };

            _context.AppSettings.Add(settings);
            await _context.SaveChangesAsync(ct);
        }

        return settings;
    }

    public async Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        // Track original values to detect progression changes
        var originalEntry = await _context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == settings.Id, ct);
        bool progressionChanged = originalEntry != null &&
                                  (originalEntry.TotalXp != settings.TotalXp ||
                                   originalEntry.UserLevel != settings.UserLevel ||
                                   originalEntry.Coins != settings.Coins);

        settings.UpdatedAt = DateTime.UtcNow;
        _context.AppSettings.Update(settings);
        await _context.SaveChangesAsync(ct);

        // Notify subscribers if progression data changed
        if (progressionChanged)
        {
            OnProgressionChanged();
        }
    }

    public async Task<int> GetUserCoinsAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        return settings.Coins;
    }

    public async Task<int> GetUserLevelAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        return settings.UserLevel;
    }

    public async Task SpendCoinsAsync(int amount, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);

        if (settings.Coins < amount)
            throw new InvalidOperationException($"Not enough coins. Have {settings.Coins}, need {amount}");

        settings.Coins -= amount;
        await UpdateSettingsAsync(settings, ct);
    }

    public async Task AddCoinsAsync(int amount, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        settings.Coins += amount;
        await UpdateSettingsAsync(settings, ct);
    }

    /// <summary>
    /// Recalculates and updates the UserLevel based on TotalXp.
    /// Use this to fix corrupted level data.
    /// </summary>
    public async Task RecalculateUserLevelAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);

        // Calculate correct level from total XP
        int correctLevel = XpCalculator.CalculateLevelFromXp(settings.TotalXp);

        // Update if different
        if (settings.UserLevel != correctLevel)
        {
            settings.UserLevel = correctLevel;
            await UpdateSettingsAsync(settings, ct);
        }
    }
}
