using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Resources;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>Base class for all ViewModels.</summary>
public abstract partial class ViewModelBase : ObservableObject, IDisposable
{
    /// <summary>Per-load cancellation source: each ct-accepting Execute call starts a fresh scope and cancels the previous in-flight load so it can't race through the shared DbContext.</summary>
    private CancellationTokenSource? _loadCts;
    private bool _disposed;
    /// <summary>Ambient crash reporter for forwarding caught exceptions as non-fatals; set at startup, NoOp in tests.</summary>
    public static ICrashReportingService CrashReporter { get; set; } = NoOpCrashReportingService.Instance;

    /// <summary>Ambient localizer for fallback error prefixes and the DB-timeout message; set at startup, null in tests.</summary>
    public static IStringLocalizer<AppResources>? Localizer { get; set; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>True when the error reflects a failed/timed-out DB init; pages use it to show a retry button.</summary>
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

    /// <summary>Looks up <paramref name="key"/> via the ambient <see cref="Localizer"/>; returns <paramref name="fallback"/> (or the key) when localization isn't wired up.</summary>
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

    /// <summary>Executes an action with error handling and busy-state management.</summary>
    protected Task ExecuteSafelyAsync(Func<Task> action, string? errorPrefix = null)
        => ExecuteCoreAsync(_ => action(), errorPrefix, ensureDb: false, CancellationToken.None);

    /// <summary>Executes an action after ensuring the DB is initialized; call from Load methods to avoid race conditions.</summary>
    protected Task ExecuteSafelyWithDbAsync(Func<Task> action, string? errorPrefix = null)
        => ExecuteCoreAsync(_ => action(), errorPrefix, ensureDb: true, CancellationToken.None);

    /// <summary>Runs <paramref name="action"/> under a fresh per-load scope, passing its token so teardown or the next load cancels this one.</summary>
    protected Task ExecuteSafelyAsync(Func<CancellationToken, Task> action, string? errorPrefix = null)
        => ExecuteCoreAsync(action, errorPrefix, ensureDb: false, BeginLoadScope());

    /// <summary>DB-gated variant that threads a fresh per-load token into the action.</summary>
    protected Task ExecuteSafelyWithDbAsync(Func<CancellationToken, Task> action, string? errorPrefix = null)
        => ExecuteCoreAsync(action, errorPrefix, ensureDb: true, BeginLoadScope());

    /// <summary>Error wrapper for fire-and-forget event callbacks that must NOT touch shared IsBusy/ErrorMessage; exceptions still go to the crash reporter.</summary>
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
            // Load superseded or cancelled on teardown — swallow and leave UI state to its replacement.
            System.Diagnostics.Debug.WriteLine("Load cancelled (superseded or disposed).");
        }
        catch (Exception ex) when (loadToken.IsCancellationRequested)
        {
            // A superseded load failed with a non-cancellation exception (likely the shared DbContext's "second operation" error); report as non-fatal, don't clobber the active load's state.
            ReportNonFatal(ex, errorPrefix, source: ensureDb ? "viewmodel_db_superseded" : "viewmodel_superseded");
        }
        catch (TimeoutException tex) when (ensureDb)
        {
            // Mark failed so IsDatabaseInitializationFailed (and the retry UI) reflects the timeout.
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
            // Don't clear IsBusy for a superseded load — the newer load owns it; clearing flickers the spinner.
            if (!loadToken.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>Cancels the previous in-flight load and starts a fresh scope, returning its token. Previous source is cancelled (not disposed) to avoid racing an EF query holding it.</summary>
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

    /// <summary>Cancels the currently running scoped load (e.g. from a page's teardown hook). Safe no-op when no load is running.</summary>
    public void CancelOngoingLoad()
    {
        try
        {
            _loadCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed by the completing load's Dispose() — nothing to cancel.
        }
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

