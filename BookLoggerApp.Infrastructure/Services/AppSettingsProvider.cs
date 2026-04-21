using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Core.Helpers;

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
    public event EventHandler? SettingsChanged;

    public AppSettingsProvider(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Raises the ProgressionChanged event to notify subscribers of progression data changes.
    /// Subscriber exceptions are caught and logged so that a single buggy handler cannot
    /// take down the caller (e.g. mid-restore, where a stale DbContext in one subscriber
    /// used to blow up the whole backup-restore flow).
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
    /// Raises the SettingsChanged event to notify subscribers of settings changes.
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
    /// Returns the current AppSettings, either from the in-memory cache (fresh within
    /// <see cref="_cacheLifetime"/>) or newly loaded from the database.
    ///
    /// <para><b>Mutability contract:</b> the returned instance is shared by reference with
    /// the cache. Callers that mutate fields (e.g. <c>settings.TotalXp += …</c>) MUST pair
    /// that with a corresponding <see cref="UpdateSettingsAsync"/> call so the RowVersion
    /// stays in sync. This is intentional — <see cref="ProgressionService"/> relies on the
    /// shared-reference pattern to apply XP + level + coin updates atomically inside a
    /// single save. If you need an isolated snapshot, call <see cref="InvalidateCache"/>
    /// first to force a fresh load.</para>
    ///
    /// <para><b>Thread safety:</b> not guarded. The single-user app serialises all
    /// progression writes through ProgressionService; no other call path mutates fields
    /// concurrently.</para>
    /// </summary>
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
                Coins = 100, // Start with 100 coins
                OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion,
                OnboardingIntroStatus = OnboardingIntroStatus.NotStarted
            };

            context.AppSettings.Add(settings);
            await context.SaveChangesAsync(ct);
        }

        // Update cache
        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        return settings;
    }

    /// <summary>
    /// Persists the given settings instance. The RowVersion on <paramref name="settings"/>
    /// must match the DB's current row version, otherwise EF raises
    /// <see cref="DbUpdateConcurrencyException"/> — the caller either just loaded the
    /// settings via <see cref="GetSettingsAsync"/> (cache hit) or already went through an
    /// invalidation. The saved instance becomes the new cache entry (shared reference);
    /// see <see cref="GetSettingsAsync"/> for the mutability contract.
    /// </summary>
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

        // Refresh the cached reference so subsequent GetSettingsAsync calls see the saved state
        _cachedSettings = settings;
        _lastLoad = DateTime.UtcNow;

        // Notify subscribers if progression data changed
        if (progressionChanged)
        {
            OnProgressionChanged();
        }

        // Notify subscribers that settings changed
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

        // Invalidate cache
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

        // Invalidate cache
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
