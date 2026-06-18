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
public abstract partial class ViewModelBase : ObservableObject
{
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
    /// CODE_REVIEW CQ-01: the token-accepting <see cref="ExecuteSafelyAsync(Func{CancellationToken, Task}, string?)"/>
    /// / <see cref="ExecuteSafelyWithDbAsync(Func{CancellationToken, Task}, string?)"/> overloads run each load under
    /// this source. Starting a new scoped load cancels the previous one (supersede-on-reload), and
    /// <see cref="CancelOngoingLoad"/> lets a page cancel the in-flight load on teardown. The token then flows
    /// through the service + repository layers (which now honour it) down to EF, so screen loads are cancellable
    /// end-to-end. ViewModelBase deliberately does NOT implement IDisposable: the ViewModels are registered as
    /// transient, and a transient IDisposable would be retained by the DI container for its entire lifetime.
    /// </summary>
    private CancellationTokenSource? _loadCts;

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
    protected async Task ExecuteSafelyAsync(Func<Task> action, string? errorPrefix = null)
    {
        try
        {
            IsBusy = true;
            ClearError();
            await action();
        }
        catch (Exception ex)
        {
            var prefix = errorPrefix ?? (Localizer?["Error_Generic"].Value ?? "An error occurred");
            SetError($"{prefix}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"ERROR: {ex}");
            ReportNonFatal(ex, errorPrefix, source: "viewmodel");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Executes an action safely after ensuring the database is initialized.
    /// This should be called from ViewModel Load methods to prevent race conditions.
    /// Note: DbContext concurrency is now handled via Transient lifetime registration.
    /// </summary>
    protected async Task ExecuteSafelyWithDbAsync(Func<Task> action, string? errorPrefix = null)
    {
        try
        {
            IsBusy = true;
            ClearError();

            await DatabaseInitializationHelper.EnsureInitializedAsync(DatabaseInitializationHelper.DefaultTimeout);

            await action();
        }
        catch (TimeoutException tex)
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
            ReportNonFatal(ex, errorPrefix, source: "viewmodel_db");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Begins a new per-load cancellation scope, cancelling (superseding) any previous in-flight
    /// load on this ViewModel. Returns the token source whose token is handed to the load action.
    /// </summary>
    private CancellationTokenSource BeginLoadScope()
    {
        var previous = _loadCts;
        var fresh = new CancellationTokenSource();
        _loadCts = fresh;

        if (previous is not null)
        {
            // Cancel the superseded load; its own ExecuteSafely* finally disposes its token source.
            try
            {
                previous.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The previous load already completed and disposed its source — nothing to cancel.
            }
        }

        return fresh;
    }

    /// <summary>
    /// Cancels the currently running scoped load, if any. The primary cancellation mechanism is
    /// supersede-on-reload (a new scoped load cancels the previous one); this method additionally
    /// lets a page cancel an in-flight load on teardown — a page that wants that behaviour must call
    /// it from its own <c>Dispose</c>/navigation hook (no page wires this up yet). Safe no-op when no
    /// load is running.
    /// </summary>
    public void CancelOngoingLoad()
    {
        try
        {
            _loadCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed by the completing load's finally — nothing to do.
        }
    }

    /// <summary>
    /// Token-aware variant of <see cref="ExecuteSafelyAsync(Func{Task}, string?)"/>. Hands the
    /// per-load cancellation token to <paramref name="action"/> so it can be threaded into the
    /// service/repository calls. A superseded or cancelled load is swallowed silently (not an error).
    /// </summary>
    protected async Task ExecuteSafelyAsync(Func<CancellationToken, Task> action, string? errorPrefix = null)
    {
        var cts = BeginLoadScope();
        try
        {
            IsBusy = true;
            ClearError();
            await action(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Superseded by a newer load or cancelled on teardown — not a user-facing error.
        }
        catch (Exception ex) when (!IsActiveScope(cts))
        {
            // This load was superseded by a newer one; its failure (e.g. a stale DbContext
            // throwing a non-cancellation exception) must not clobber the active load's state.
            ReportNonFatal(ex, errorPrefix, source: "viewmodel_superseded");
        }
        catch (Exception ex)
        {
            var prefix = errorPrefix ?? (Localizer?["Error_Generic"].Value ?? "An error occurred");
            SetError($"{prefix}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"ERROR: {ex}");
            ReportNonFatal(ex, errorPrefix, source: "viewmodel");
        }
        finally
        {
            FinishLoadScope(cts);
        }
    }

    /// <summary>
    /// Token-aware variant of <see cref="ExecuteSafelyWithDbAsync(Func{Task}, string?)"/>.
    /// </summary>
    protected async Task ExecuteSafelyWithDbAsync(Func<CancellationToken, Task> action, string? errorPrefix = null)
    {
        var cts = BeginLoadScope();
        try
        {
            IsBusy = true;
            ClearError();

            await DatabaseInitializationHelper.EnsureInitializedAsync(DatabaseInitializationHelper.DefaultTimeout);

            await action(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Superseded by a newer load or cancelled on teardown — not a user-facing error.
        }
        catch (Exception ex) when (!IsActiveScope(cts))
        {
            // Superseded load: do not clobber the active load's ErrorMessage/IsBusy (see above).
            ReportNonFatal(ex, errorPrefix, source: "viewmodel_db_superseded");
        }
        catch (TimeoutException tex)
        {
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
            ReportNonFatal(ex, errorPrefix, source: "viewmodel_db");
        }
        finally
        {
            FinishLoadScope(cts);
        }
    }

    /// <summary>
    /// True while <paramref name="cts"/> is still the active load scope, i.e. no newer load has
    /// superseded it. Used to keep a superseded load from writing the active load's error/busy state.
    /// </summary>
    private bool IsActiveScope(CancellationTokenSource cts) => ReferenceEquals(_loadCts, cts);

    /// <summary>
    /// Cleans up after a scoped load: clears the busy flag and the active token source only when
    /// this load still owns it (a newer load may have superseded it). The completing load always
    /// disposes its own token source exactly once.
    /// </summary>
    private void FinishLoadScope(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_loadCts, cts))
        {
            IsBusy = false;
            _loadCts = null;
        }

        cts.Dispose();
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

