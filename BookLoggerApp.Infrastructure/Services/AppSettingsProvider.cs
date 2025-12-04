using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Provider implementation for app settings using DbContextFactory for thread-safe operations.
/// </summary>
public class AppSettingsProvider : IAppSettingsProvider
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private AppSettings? _cachedSettings;
    private DateTime _lastLoad = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

    public event EventHandler? ProgressionChanged;

    public AppSettingsProvider(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
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
        // Return cached settings if still valid
        if (_cachedSettings != null && DateTime.UtcNow - _lastLoad < _cacheLifetime)
            return _cachedSettings;

        // Create a new context for this operation
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Load from database
        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);

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

            context.AppSettings.Add(settings);
            await context.SaveChangesAsync(ct);
        }

        // Update cache
        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        return settings;
    }

    public async Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Track original values to detect progression changes
        var originalEntry = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == settings.Id, ct);
        bool progressionChanged = originalEntry != null &&
                                  (originalEntry.TotalXp != settings.TotalXp ||
                                   originalEntry.UserLevel != settings.UserLevel ||
                                   originalEntry.Coins != settings.Coins);

        settings.UpdatedAt = DateTime.UtcNow;
        context.AppSettings.Update(settings);
        await context.SaveChangesAsync(ct);

        // Invalidate cache on update
        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

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
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        if (settings.Coins < amount)
            throw new InsufficientFundsException(amount, settings.Coins);

        settings.Coins -= amount;
        settings.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        // Invalidate cache
        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        OnProgressionChanged();
    }

    public async Task AddCoinsAsync(int amount, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        settings.Coins += amount;
        settings.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        // Invalidate cache
        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        OnProgressionChanged();
    }

    public async Task IncrementPlantsPurchasedAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        settings.PlantsPurchased++;
        settings.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        // Invalidate cache
        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;
    }

    public async Task<int> GetPlantsPurchasedAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        return settings.PlantsPurchased;
    }

    public void InvalidateCache()
    {
        _cachedSettings = null;
        _lastLoad = DateTime.MinValue;
        OnProgressionChanged();
    }

    /// <summary>
    /// Recalculates and updates the UserLevel based on TotalXp.
    /// Use this to fix corrupted level data.
    /// </summary>
    public async Task RecalculateUserLevelAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        // Calculate correct level from total XP
        int correctLevel = XpCalculator.CalculateLevelFromXp(settings.TotalXp);

        // Update if different
        if (settings.UserLevel != correctLevel)
        {
            settings.UserLevel = correctLevel;
            settings.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            // Invalidate cache
            _cachedSettings = settings;
            _lastLoad = DateTime.UtcNow;

            OnProgressionChanged();
        }
    }
}
