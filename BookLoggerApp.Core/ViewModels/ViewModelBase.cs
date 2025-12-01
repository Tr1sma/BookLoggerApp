using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// Base class for all ViewModels.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

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

            // Wait for database initialization to complete
            await BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.EnsureInitializedAsync();

            await action();
        }
        catch (Exception ex)
        {
            var prefix = errorPrefix ?? "An error occurred";
            SetError($"{prefix}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

