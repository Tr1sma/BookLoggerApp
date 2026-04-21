namespace BookLoggerApp.Core.Infrastructure;

/// <summary>
/// Helper class to track database initialization status.
/// This class is in Core to avoid circular dependencies.
/// The actual initialization is done by DbInitializer in the Infrastructure layer.
/// </summary>
public static class DatabaseInitializationHelper
{
    private static readonly TaskCompletionSource<bool> _initializationTcs = new();
    private static bool _isInitialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Waits for database initialization to complete.
    /// </summary>
    public static async Task EnsureInitializedAsync()
    {
        await _initializationTcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if the database has been initialized.
    /// </summary>
    public static bool IsInitialized
    {
        get
        {
            lock (_lock)
            {
                return _isInitialized;
            }
        }
    }

    /// <summary>
    /// Marks the database as initialized successfully.
    /// This should be called by the Infrastructure layer's DbInitializer.
    /// Idempotent — a second call becomes a no-op rather than crashing.
    /// </summary>
    public static void MarkAsInitialized()
    {
        lock (_lock)
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
        }

        _initializationTcs.TrySetResult(true);
    }

    /// <summary>
    /// Marks the database initialization as failed.
    /// This should be called by the Infrastructure layer's DbInitializer.
    /// Idempotent — calling this after MarkAsInitialized or a previous MarkAsFailed
    /// is a no-op instead of crashing with InvalidOperationException.
    /// </summary>
    public static void MarkAsFailed(Exception exception)
    {
        _initializationTcs.TrySetException(exception);
    }
}
