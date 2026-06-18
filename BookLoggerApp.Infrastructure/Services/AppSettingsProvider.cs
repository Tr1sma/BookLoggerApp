using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

public class AppSettingsProvider : IAppSettingsProvider
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private AppSettings? _cachedSettings;
    private DateTime _lastLoad = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    // Guards against infinite recovery loops after schema repair.
    private int _repairAttempted;

    public event EventHandler? ProgressionChanged;
    public event EventHandler? SettingsChanged;

    // No ICrashReportingService here — it creates a DI cycle:
    // FirebaseCrashlyticsService → IAnalyticsConsentGate → IAppSettingsProvider.
    public AppSettingsProvider(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Swallows handler exceptions so one buggy subscriber can't crash the caller.
    /// </summary>
    private void OnProgressionChanged()
    {
        var handlers = ProgressionChanged;
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler)handler).Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProgressionChanged handler threw: {ex}");
            }
        }
    }

    /// <summary>
    /// Swallows handler exceptions so one buggy subscriber can't crash the caller.
    /// </summary>
    private void OnSettingsChanged()
    {
        var handlers = SettingsChanged;
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler)handler).Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsChanged handler threw: {ex}");
            }
        }
    }

    /// <summary>
    /// Returns cached settings (5-min TTL) or loads from DB.
    /// Returned instance is shared by reference — mutating callers must call
    /// <see cref="UpdateSettingsAsync"/> to keep RowVersion in sync.
    /// </summary>
    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        if (_cachedSettings != null && DateTime.UtcNow - _lastLoad < _cacheLifetime)
            return _cachedSettings;

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await LoadSettingsWithRecoveryAsync(context, ct);

        if (settings == null)
        {
            settings = new AppSettings
            {
                Theme = "Light",
                Language = DetectSystemLanguage(),
                UserLevel = 1,
                TotalXp = 0,
                Coins = 100,
                OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion,
                OnboardingIntroStatus = OnboardingIntroStatus.NotStarted
            };

            context.AppSettings.Add(settings);
            await context.SaveChangesAsync(ct);
        }

        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        return settings;
    }

    /// <summary>
    /// Persists settings; caller must hold the current RowVersion to avoid concurrency exceptions.
    /// </summary>
    public async Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var originalEntry = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == settings.Id, ct);
        bool progressionChanged = originalEntry != null &&
                                  (originalEntry.TotalXp != settings.TotalXp ||
                                   originalEntry.UserLevel != settings.UserLevel ||
                                   originalEntry.Coins != settings.Coins);

        settings.UpdatedAt = DateTime.UtcNow;
        context.AppSettings.Update(settings);
        await context.SaveChangesAsync(ct);

        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        if (progressionChanged)
        {
            OnProgressionChanged();
        }

        OnSettingsChanged();
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
        ValidateAmount(amount);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        if (settings.Coins < amount)
            throw new InsufficientFundsException(amount, settings.Coins);

        settings.Coins -= amount;
        settings.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        OnProgressionChanged();
    }

    public async Task AddCoinsAsync(int amount, CancellationToken ct = default)
    {
        ValidateAmount(amount);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        settings.Coins += amount;
        settings.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        OnProgressionChanged();
    }

    private static void ValidateAmount(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        }
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
        InvalidateCache(true);
    }

    public void InvalidateCache(bool notifyProgressionChanged)
    {
        _cachedSettings = null;
        _lastLoad = DateTime.MinValue;
        if (notifyProgressionChanged)
        {
            OnProgressionChanged();
        }
    }

    public void SetCachedSettings(AppSettings settings)
    {
        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;
    }

    /// <summary>
    /// Retries once after a "no such column" SQLite error via <see cref="SchemaDriftGuard"/>.
    /// Primary repair is in <see cref="DbInitializer"/>; this is a belt-and-braces fallback.
    /// </summary>
    private async Task<AppSettings?> LoadSettingsWithRecoveryAsync(AppDbContext context, CancellationToken ct)
    {
        try
        {
            return await context.AppSettings.FirstOrDefaultAsync(ct);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("no such column"))
        {
            if (Interlocked.Exchange(ref _repairAttempted, 1) != 0)
            {
                // Already repaired once — rethrow to surface the real failure.
                throw;
            }

            System.Diagnostics.Debug.WriteLine($"AppSettingsProvider: caught drift '{ex.Message}'; invoking SchemaDriftGuard.");

            // Null crashReporting avoids the DI cycle; see constructor comment.
            await SchemaDriftGuard.EnsureCriticalColumnsAsync(context, crashReporting: null, logger: null, ct);

            return await context.AppSettings.FirstOrDefaultAsync(ct);
        }
    }

    /// <summary>
    /// Fixes corrupted level data by recalculating from TotalXp.
    /// </summary>
    public async Task RecalculateUserLevelAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
            throw new InvalidOperationException("AppSettings not found");

        int correctLevel = XpCalculator.CalculateLevelFromXp(settings.TotalXp);

        if (settings.UserLevel != correctLevel)
        {
            settings.UserLevel = correctLevel;
            settings.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            _cachedSettings = settings;
            _lastLoad = DateTime.UtcNow;

            OnProgressionChanged();
        }
    }

    /// <summary>
    /// Returns "de" for German system culture, "en" otherwise. Exceptions fall back to "en".
    /// </summary>
    private static string DetectSystemLanguage()
    {
        try
        {
            string iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return string.Equals(iso, "de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        }
        catch
        {
            return "en";
        }
    }
}
