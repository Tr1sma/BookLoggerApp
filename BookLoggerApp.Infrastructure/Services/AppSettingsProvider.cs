using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>App settings provider; uses DbContextFactory for thread-safe operations.</summary>
public class AppSettingsProvider : IAppSettingsProvider
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private AppSettings? _cachedSettings;
    private DateTime _lastLoad = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    // Serialises read-modify-write against the cached AppSettings so concurrent mutators can't
    // interleave and lose an update (CODE_REVIEW BUG-08 / INK-03 / SEC-12). NOT reentrant — never
    // call a gated method while holding the gate; raise change events only after releasing it.
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    // Guards against infinite recovery loops: if the schema repair didn't fix the
    // "no such column" error, rethrow on the next hit instead of looping forever.
    private int _repairAttempted;

    public event EventHandler? ProgressionChanged;
    public event EventHandler? SettingsChanged;

    // Do NOT add ICrashReportingService as a ctor param: it creates a DI cycle on Android
    // (FirebaseCrashlyticsService → IAnalyticsConsentGate → IAppSettingsProvider → here).
    // DbInitializer's startup guard already reports non-fatals for the main path.
    public AppSettingsProvider(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Raises ProgressionChanged. Subscriber exceptions are caught and logged so one buggy
    /// handler can't take down the caller (e.g. a stale DbContext mid-restore).
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

    /// <summary>Raises SettingsChanged.</summary>
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
    /// Returns current AppSettings from cache (fresh within <see cref="_cacheLifetime"/>) or the DB.
    /// <para>Mutability: the returned instance is shared by reference with the cache; callers that
    /// mutate fields MUST follow with <see cref="UpdateSettingsAsync"/> to keep RowVersion in sync.
    /// Call <see cref="InvalidateCache"/> first if you need an isolated snapshot.</para>
    /// <para>Thread safety: all write paths plus the cache-miss load serialise behind
    /// <see cref="_writeGate"/> (CODE_REVIEW BUG-08 / INK-03), preventing lost updates.</para>
    /// </summary>
    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        if (_cachedSettings != null && DateTime.UtcNow - _lastLoad < _cacheLifetime)
            return _cachedSettings;

        // Slow path: serialise so two callers can't both miss the cache and each insert a default row.
        await _writeGate.WaitAsync(ct);
        try
        {
            // Double-check: another caller may have filled the cache while we waited.
            if (_cachedSettings != null && DateTime.UtcNow - _lastLoad < _cacheLifetime)
                return _cachedSettings;

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Last-chance schema-drift repair in case DbInitializer's startup guard missed a column.
            var settings = await LoadSettingsWithRecoveryAsync(context, ct);

            if (settings == null)
            {
                settings = new AppSettings
                {
                    Theme = "Light",
                    Language = DetectSystemLanguage(),
                    UserLevel = 1,
                    TotalXp = 0,
                    Coins = 100, // Start with 100 coins
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
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Persists the given settings. RowVersion must match the DB row or EF raises
    /// <see cref="DbUpdateConcurrencyException"/>. The saved instance becomes the new cache entry
    /// (shared reference); see <see cref="GetSettingsAsync"/> for the mutability contract.
    /// </summary>
    public async Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        bool progressionChanged;

        await _writeGate.WaitAsync(ct);
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Detect progression changes by comparing against the persisted row.
            var originalEntry = await context.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == settings.Id, ct);
            progressionChanged = originalEntry != null &&
                                      (originalEntry.TotalXp != settings.TotalXp ||
                                       originalEntry.UserLevel != settings.UserLevel ||
                                       originalEntry.Coins != settings.Coins);

            settings.UpdatedAt = DateTime.UtcNow;
            context.AppSettings.Update(settings);
            await context.SaveChangesAsync(ct);

            _cachedSettings = settings;
            _lastLoad = DateTime.UtcNow;
        }
        finally
        {
            _writeGate.Release();
        }

        // Notify outside the gate (a handler may call back into the provider).
        if (progressionChanged)
        {
            OnProgressionChanged();
        }

        OnSettingsChanged();
    }

    /// <summary>
    /// Mirrors entitlement tier/expiry into AppSettings via a FRESH tracked row, updating only
    /// CurrentTier + EntitlementExpiresAt so the narrow UPDATE can't overwrite a concurrent
    /// XP/coin/level award (CODE_REVIEW SEC-12). Patches those two columns + RowVersion into the
    /// shared cache so a held reference stays valid for a later <see cref="UpdateSettingsAsync"/>.
    /// </summary>
    public async Task UpdateEntitlementMirrorAsync(SubscriptionTier tier, DateTime? expiresAt, CancellationToken ct = default)
    {
        bool changed = false;

        await _writeGate.WaitAsync(ct);
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
            if (settings is null)
            {
                return;
            }

            if (settings.CurrentTier == tier && Nullable.Equals(settings.EntitlementExpiresAt, expiresAt))
            {
                return;
            }

            settings.CurrentTier = tier;
            settings.EntitlementExpiresAt = expiresAt;
            settings.UpdatedAt = DateTime.UtcNow;
            // Only these columns changed → narrow UPDATE (RowVersion bumped by StampRowVersions;
            // TotalXp/Coins/UserLevel untouched).
            await context.SaveChangesAsync(ct);
            changed = true;

            // Keep a held cache reference coherent: mirror the two columns and adopt the new RowVersion.
            if (_cachedSettings is not null)
            {
                _cachedSettings.CurrentTier = settings.CurrentTier;
                _cachedSettings.EntitlementExpiresAt = settings.EntitlementExpiresAt;
                _cachedSettings.RowVersion = settings.RowVersion;
            }
        }
        finally
        {
            _writeGate.Release();
        }

        if (changed)
        {
            OnSettingsChanged();
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
        ValidateAmount(amount);

        await _writeGate.WaitAsync(ct);
        try
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

            _cachedSettings = settings;
            _lastLoad = DateTime.UtcNow;
        }
        finally
        {
            _writeGate.Release();
        }

        OnProgressionChanged();
    }

    public async Task AddCoinsAsync(int amount, CancellationToken ct = default)
    {
        ValidateAmount(amount);

        await _writeGate.WaitAsync(ct);
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
            if (settings == null)
                throw new InvalidOperationException("AppSettings not found");

            settings.Coins += amount;
            settings.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            _cachedSettings = settings;
            _lastLoad = DateTime.UtcNow;
        }
        finally
        {
            _writeGate.Release();
        }

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
        await _writeGate.WaitAsync(ct);
        try
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
        finally
        {
            _writeGate.Release();
        }
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
    /// Reads AppSettings; on a SQLite "no such column" error, runs
    /// <see cref="SchemaDriftGuard.EnsureCriticalColumnsAsync"/> once and retries. Belt-and-braces
    /// behind the primary repair in <see cref="DbInitializer"/>; retries once per instance so a
    /// genuine config error can't spin forever.
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
                // Already repaired once and still failing — rethrow instead of looping.
                throw;
            }

            System.Diagnostics.Debug.WriteLine($"AppSettingsProvider: caught drift '{ex.Message}'; invoking SchemaDriftGuard.");

            // null crashReporting: taking ICrashReportingService here would create a DI cycle
            // (FirebaseCrashlyticsService → IAnalyticsConsentGate → this).
            await SchemaDriftGuard.EnsureCriticalColumnsAsync(context, crashReporting: null, logger: null, ct);

            return await context.AppSettings.FirstOrDefaultAsync(ct);
        }
    }

    /// <summary>Recalculates UserLevel from TotalXp; fixes corrupted level data.</summary>
    public async Task RecalculateUserLevelAsync(CancellationToken ct = default)
    {
        bool changed = false;

        await _writeGate.WaitAsync(ct);
        try
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
                changed = true;
            }
        }
        finally
        {
            _writeGate.Release();
        }

        if (changed)
        {
            OnProgressionChanged();
        }
    }

    /// <summary>
    /// Picks the first-launch UI language: <c>"de"</c> for a German system culture, else <c>"en"</c>.
    /// Exceptions fall back to English so startup can't fail here.
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
