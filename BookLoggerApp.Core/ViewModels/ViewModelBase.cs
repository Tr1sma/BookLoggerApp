using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Resources;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, IDisposable
{
    /// <summary>
    /// CODE_REVIEW CQ-01: per-load cancellation source. Each ct-accepting
    /// <see cref="ExecuteSafelyAsync(Func{CancellationToken, Task}, string?)"/> /
    /// <see cref="ExecuteSafelyWithDbAsync(Func{CancellationToken, Task}, string?)"/> call
    /// begins a fresh load scope: the previous in-flight load is cancelled so a torn-down
    /// or superseded screen-load no longer races the next one through the shared transient
    /// DbContext. Cancelled on <see cref="Dispose"/> too.
    /// </summary>
    private CancellationTokenSource? _loadCts;
    private bool _disposed;
    /// <summary>
    /// Ambient crash reporter used by <see cref="ExecuteSafelyAsync"/> /
    /// <see cref="ExecuteSafelyWithDbAsync"/> to forward caught exceptions as non-fatals.
    /// Assigned once at app startup by AnalyticsBootstrapper; defaults to NoOp for tests.
    /// </summary>
    public static ICrashReportingService CrashReporter { get; set; } = NoOpCrashReportingService.Instance;

    /// <summary>
    /// Ambient localizer used by <see cref="ExecuteSafelyAsync"/> and
    /// <see cref="ExecuteSafelyWithDbAsync"/> for the fallback error-prefix and the
    /// DB-timeout message. Assigned once at app startup by AnalyticsBootstrapper;
    /// defaults to null so tests can run without wiring up localization.
    /// </summary>
    public static IStringLocalizer<AppResources>? Localizer { get; set; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// True when the current error reflects a failed or timed-out database
    /// initialization. Pages use this to decide whether to show a "retry" button.
    /// </summary>
    public bool IsDatabaseInitializationFailed => DatabaseInitializationHelper.InitializationFailed;

    protected void ClearError()
    {
        ErrorMessage = null;
    }

    protected void SetError(string message)
    {
        ErrorMessage = message;
        IsBusy = false;
    }

    /// <summary>
    /// Looks up <paramref name="key"/> via the ambient <see cref="Localizer"/>.
    /// Returns <paramref name="fallback"/> (or the key itself) when localization is
    /// not wired up — e.g. in unit tests.
    /// </summary>
    protected static string Tr(string key, string? fallback = null)
    {
        var loc = Localizer;
        if (loc is null)
        {
            return fallback ?? key;
        }
        var str = loc[key];
        return str.ResourceNotFound ? (fallback ?? key) : str.Value;
    }

    protected static string Tr(string key, params object[] args)
    {
        var loc = Localizer;
        if (loc is null)
        {
            return string.Format(key, args);
        }
        var str = loc[key, args];
        return str.ResourceNotFound ? string.Format(key, args) : str.Value;
    }

    /// <summary>
    /// Executes an action safely with error handling and busy state management.
    /// </summary>
    protected Task ExecuteSafelyAsync(Func<Task> action, string? errorPrefix = null)
        => ExecuteCoreAsync(_ => action(), errorPrefix, ensureDb: false, CancellationToken.None);

    /// <summary>
    /// Executes an action safely after ensuring the database is initialized.
    /// This should be called from ViewModel Load methods to prevent race conditions.
    /// Note: DbContext concurrency is now handled via Transient lifetime registration.
    /// </summary>
    protected Task ExecuteSafelyWithDbAsync(Func<Task> action, string? errorPrefix = null)
        => ExecuteCoreAsync(_ => action(), errorPrefix, ensureDb: true, CancellationToken.None);

    /// <summary>
    /// CODE_REVIEW CQ-01 overload: runs <paramref name="action"/> under a fresh per-load scope
    /// and passes its <see cref="CancellationToken"/> in, so navigation/teardown (or the next
    /// load) cancels the in-flight load instead of letting it race through the shared DbContext.
    /// </summary>
    protected Task ExecuteSafelyAsync(Func<CancellationToken, Task> action, string? errorPrefix = null)
        => ExecuteCoreAsync(action, errorPrefix, ensureDb: false, BeginLoadScope());

    /// <summary>
    /// CODE_REVIEW CQ-01 overload: DB-gated variant that threads a fresh per-load token into the action.
    /// </summary>
    protected Task ExecuteSafelyWithDbAsync(Func<CancellationToken, Task> action, string? errorPrefix = null)
        => ExecuteCoreAsync(action, errorPrefix, ensureDb: true, BeginLoadScope());

    /// <summary>
    /// CODE_REVIEW BUG-17: error wrapper for event-driven, fire-and-forget callbacks (billing
    /// purchase / onboarding state changes) that must NOT touch the shared IsBusy/ErrorMessage.
    /// Unlike <see cref="ExecuteSafelyAsync(Func{Task}, string?)"/> this neither sets IsBusy nor
    /// clears/sets ErrorMessage, so a background event firing during an in-flight foreground load
    /// can't clobber that load's busy/overlay/error state. Caught exceptions are still forwarded
    /// to the crash reporter.
    /// </summary>
    protected async Task ExecuteDetachedAsync(Func<Task> action, string source)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR (detached {source}): {ex}");
            ReportNonFatal(ex, errorPrefix: null, source: source);
        }
    }

    private async Task ExecuteCoreAsync(Func<CancellationToken, Task> action, string? errorPrefix, bool ensureDb, CancellationToken loadToken)
    {
        try
        {
            IsBusy = true;
            ClearError();

            if (ensureDb)
            {
                await DatabaseInitializationHelper.EnsureInitializedAsync(DatabaseInitializationHelper.DefaultTimeout);
            }

            await action(loadToken);
        }
        catch (OperationCanceledException) when (loadToken.IsCancellationRequested)
        {
            // CODE_REVIEW CQ-01: this load was superseded by a newer load or cancelled on
            // teardown — swallow silently and leave the UI state to whatever replaced it.
            System.Diagnostics.Debug.WriteLine("Load cancelled (superseded or disposed).");
        }
        catch (TimeoutException tex) when (ensureDb)
        {
            // Surface the timeout to the helper too, so IsDatabaseInitializationFailed
            // reflects reality for the retry UI. Without this, pages that gate Retry()
            // on InitializationFailed would never trigger a fresh init.
            DatabaseInitializationHelper.MarkAsFailed(tex);

            var prefix = errorPrefix ?? (Localizer?["Error_GenericShort"].Value ?? "Error");
            var body = Localizer?["Error_DbInitializing"].Value
                       ?? "Database is still initializing. Please try again.";
            SetError($"{prefix}: {body}");
            System.Diagnostics.Debug.WriteLine("Timeout beim Warten auf Datenbank-Initialisierung.");
            ReportNonFatal(tex, errorPrefix, source: "viewmodel_db_timeout");
        }
        catch (Exception ex)
        {
            var prefix = errorPrefix ?? (Localizer?["Error_Generic"].Value ?? "An error occurred");
            SetError($"{prefix}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"ERROR: {ex}");
            ReportNonFatal(ex, errorPrefix, source: ensureDb ? "viewmodel_db" : "viewmodel");
        }
        finally
        {
            // Don't clear IsBusy for a superseded/cancelled load — the newer load owns the
            // busy state now, and clobbering it here would flicker the spinner off mid-load.
            if (!loadToken.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// CODE_REVIEW CQ-01: cancels the previous in-flight load and starts a fresh load scope,
    /// returning its token. The previous source is only cancelled (not disposed) here to avoid
    /// racing an EF query that still holds the token; it is GC-reclaimed. Dispose() does the
    /// final cleanup.
    /// </summary>
    protected CancellationToken BeginLoadScope()
    {
        if (_disposed)
        {
            return new CancellationToken(canceled: true);
        }

        var previous = _loadCts;
        var fresh = new CancellationTokenSource();
        _loadCts = fresh;
        previous?.Cancel();
        return fresh.Token;
    }

    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_loadCts is not null)
        {
            try
            {
                _loadCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // already disposed elsewhere — nothing to cancel
            }
            _loadCts.Dispose();
            _loadCts = null;
        }

        GC.SuppressFinalize(this);
    }

    private static void ReportNonFatal(Exception ex, string? errorPrefix, string source)
    {
        try
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = source,
                ["prefix"] = errorPrefix ?? string.Empty
            };
            CrashReporter.RecordNonFatal(ex, keys);
        }
        catch (Exception reportEx)
        {
            System.Diagnostics.Debug.WriteLine($"CrashReporter.RecordNonFatal failed: {reportEx}");
        }
    }
}

