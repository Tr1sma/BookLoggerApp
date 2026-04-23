using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
            var prefix = errorPrefix ?? "An error occurred";
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
            var prefix = errorPrefix ?? "Fehler";
            SetError($"{prefix}: Datenbank wird noch vorbereitet. Bitte versuche es erneut.");
            System.Diagnostics.Debug.WriteLine("Timeout beim Warten auf Datenbank-Initialisierung.");
            ReportNonFatal(tex, errorPrefix, source: "viewmodel_db_timeout");
        }
        catch (Exception ex)
        {
            var prefix = errorPrefix ?? "An error occurred";
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

