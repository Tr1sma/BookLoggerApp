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

