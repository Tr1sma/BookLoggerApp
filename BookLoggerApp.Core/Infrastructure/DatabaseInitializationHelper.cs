namespace BookLoggerApp.Core.Infrastructure;

/// <summary>
/// Helper class to track database initialization status.
/// This class is in Core to avoid circular dependencies.
/// The actual initialization is done by DbInitializer in the Infrastructure layer.
/// </summary>
public static class DatabaseInitializationHelper
{
    /// <summary>
    /// Default timeout applied by <see cref="EnsureInitializedAsync()"/>. With the
    /// fire-and-forget init moved onto a dedicated high-priority thread and the
    /// SchemaDriftGuard work measured, 20s is plenty of headroom even on slow
    /// budget Android devices while dramatically improving the worst-case UX
    /// when something genuinely hangs (shorter wait → working retry surfaces faster).
    /// </summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(20);

    // RunContinuationsAsynchronously prevents every awaiting ViewModel from
    // resuming synchronously on the DB-init thread when MarkAsInitialized fires.
    // Important for Blazor Hybrid: continuations that ultimately touch UI state
    // should not hijack the worker thread that just finished the migration.
    private static TaskCompletionSource<bool> _initializationTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Completes when DbInitializer's deferred background maintenance
    // (RunDeferredMaintenanceAsync) has finished. The restore path awaits this so it
    // never swaps the DB file while that fire-and-forget context is still writing — a
    // surviving second connection across the swap corrupts the WAL-index ("database disk
    // image is malformed"). MarkAsInitialized only signals the *migration* gate; deferred
    // maintenance keeps writing afterwards.
    private static TaskCompletionSource<bool> _deferredMaintenanceTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // In-process flag the Android widget (WidgetDataService — a BroadcastReceiver with its
    // own non-DI connection that ClearAllPools cannot reach) checks before opening the DB,
    // so a widget refresh cannot race a restore's file swap.
    private static volatile bool _restoreInProgress;

    private static bool _isInitialized;
    private static bool _initializationFailed;
    private static Exception? _initializationException;
    private static readonly object _lock = new();

    /// <summary>
    /// In-memory log buffer that captures DB initialization timings and errors.
    /// Exposed through <c>IMigrationService.GetMigrationLog()</c> so users can
    /// read and share the log from the Settings page when reporting issues.
    /// Kept as a single static buffer so it's accessible from the Infrastructure
    /// layer (DbInitializer) without introducing a new abstraction.
    /// </summary>
    public static System.Text.StringBuilder InitLog { get; } = new();

    /// <summary>
    /// Appends a timestamped line to <see cref="InitLog"/>. Safe to call from
    /// any thread.
    /// </summary>
    public static void AppendInitLog(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (InitLog)
        {
            InitLog.AppendLine(line);
        }
        System.Diagnostics.Debug.WriteLine(line);
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
    /// True when <see cref="MarkAsFailed"/> has been called and the state has not
    /// been cleared via <see cref="ResetForRetry"/>. Used to decide whether to
    /// offer a retry path in the UI.
    /// </summary>
    public static bool InitializationFailed
    {
        get
        {
            lock (_lock)
            {
                return _initializationFailed;
            }
        }
    }

    /// <summary>
    /// The exception captured by the most recent <see cref="MarkAsFailed"/> call,
    /// or null when no failure is currently recorded.
    /// </summary>
    public static Exception? InitializationException
    {
        get
        {
            lock (_lock)
            {
                return _initializationException;
            }
        }
    }

    /// <summary>
    /// Waits for database initialization using <see cref="DefaultTimeout"/>.
    /// </summary>
    public static Task EnsureInitializedAsync()
    {
        return EnsureInitializedAsync(DefaultTimeout, CancellationToken.None);
    }

    /// <summary>
    /// Waits for database initialization to complete. Throws
    /// <see cref="TimeoutException"/> when the wait exceeds <paramref name="timeout"/>,
    /// <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/>
    /// is cancelled, or the stored exception when <see cref="MarkAsFailed"/> has been
    /// called.
    /// </summary>
    public static async Task EnsureInitializedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Task<bool> tcsTask;
        lock (_lock)
        {
            tcsTask = _initializationTcs.Task;
        }

        if (tcsTask.IsCompleted)
        {
            await tcsTask.ConfigureAwait(false);
            return;
        }

        using CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task delayTask = Task.Delay(timeout, delayCts.Token);

        Task completed = await Task.WhenAny(tcsTask, delayTask).ConfigureAwait(false);
        if (completed == tcsTask)
        {
            delayCts.Cancel();
            await tcsTask.ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException("Datenbank-Initialisierung hat zu lange gedauert.");
    }

    /// <summary>
    /// True while a backup restore is swapping the DB file. The Android widget consults
    /// this before opening its own connection so a BroadcastReceiver refresh cannot race
    /// the file swap.
    /// </summary>
    public static bool IsRestoreInProgress => _restoreInProgress;

    /// <summary>Marks the start of a restore (blocks widget DB access).</summary>
    public static void BeginRestore() => _restoreInProgress = true;

    /// <summary>Marks the end of a restore (re-enables widget DB access). Idempotent.</summary>
    public static void EndRestore() => _restoreInProgress = false;

    /// <summary>
    /// Signals that DbInitializer's deferred background maintenance has finished. Called by
    /// the Infrastructure layer. Idempotent — a second call is a no-op.
    /// </summary>
    public static void MarkDeferredMaintenanceComplete()
    {
        TaskCompletionSource<bool> tcs;
        lock (_lock)
        {
            tcs = _deferredMaintenanceTcs;
        }
        tcs.TrySetResult(true);
    }

    /// <summary>
    /// Waits (best-effort) for deferred startup maintenance to complete. Unlike
    /// <see cref="EnsureInitializedAsync()"/> this NEVER throws on timeout: if maintenance
    /// is slow the caller proceeds anyway (the restore's other safeguards still apply);
    /// blocking the restore indefinitely would be worse UX. Returns immediately when the
    /// gate is already signalled.
    /// </summary>
    public static async Task EnsureDeferredMaintenanceCompleteAsync(TimeSpan timeout)
    {
        Task<bool> tcsTask;
        lock (_lock)
        {
            tcsTask = _deferredMaintenanceTcs.Task;
        }

        if (tcsTask.IsCompleted)
        {
            return;
        }

        using var delayCts = new CancellationTokenSource();
        Task delayTask = Task.Delay(timeout, delayCts.Token);
        Task completed = await Task.WhenAny(tcsTask, delayTask).ConfigureAwait(false);
        if (completed == tcsTask)
        {
            delayCts.Cancel();
        }
    }

    /// <summary>
    /// Marks the database as initialized successfully.
    /// This should be called by the Infrastructure layer's DbInitializer.
    /// Idempotent — a second call becomes a no-op rather than crashing.
    /// </summary>
    public static void MarkAsInitialized()
    {
        TaskCompletionSource<bool> tcs;
        lock (_lock)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            _initializationFailed = false;
            _initializationException = null;
            tcs = _initializationTcs;
        }

        tcs.TrySetResult(true);
    }

    /// <summary>
    /// Marks the database initialization as failed.
    /// This should be called by the Infrastructure layer's DbInitializer.
    /// Idempotent — calling this after MarkAsInitialized or a previous MarkAsFailed
    /// is a no-op instead of crashing with InvalidOperationException.
    /// </summary>
    public static void MarkAsFailed(Exception exception)
    {
        TaskCompletionSource<bool> tcs;
        lock (_lock)
        {
            if (_isInitialized)
            {
                return;
            }

            _initializationFailed = true;
            _initializationException = exception;
            tcs = _initializationTcs;
        }

        tcs.TrySetException(exception);
    }

    /// <summary>
    /// Clears a failed state and installs a fresh <see cref="TaskCompletionSource{TResult}"/>
    /// so a caller can re-run the initialization. No-op when initialization has
    /// already succeeded (retrying a success makes no sense).
    /// </summary>
    public static void ResetForRetry()
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                return;
            }

            _initializationFailed = false;
            _initializationException = null;
            _initializationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _deferredMaintenanceTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _restoreInProgress = false;
        }
    }

    /// <summary>
    /// Unconditional reset exposed to the test assembly so individual tests start
    /// from a clean state regardless of any prior <see cref="MarkAsInitialized"/> call.
    /// </summary>
    internal static void ResetForTests()
    {
        lock (_lock)
        {
            _isInitialized = false;
            _initializationFailed = false;
            _initializationException = null;
            _initializationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _deferredMaintenanceTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _restoreInProgress = false;
        }
    }
}
