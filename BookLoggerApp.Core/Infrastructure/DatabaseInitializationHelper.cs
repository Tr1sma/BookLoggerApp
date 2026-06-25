namespace BookLoggerApp.Core.Infrastructure;

/// <summary>
/// Tracks database initialization status. Lives in Core to avoid circular dependencies;
/// actual initialization runs in DbInitializer (Infrastructure layer).
/// </summary>
public static class DatabaseInitializationHelper
{
    /// <summary>
    /// Default timeout for <see cref="EnsureInitializedAsync()"/>. 20s gives headroom on slow
    /// Android devices while surfacing a retry quickly if init genuinely hangs.
    /// </summary>
    public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(20);

    // RunContinuationsAsynchronously: awaiting ViewModels must not resume synchronously on the
    // DB-init thread when MarkAsInitialized fires (Blazor Hybrid — UI continuations would hijack it).
    private static TaskCompletionSource<bool> _initializationTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Completes when DbInitializer's deferred background maintenance finishes. Restore awaits this
    // so it never swaps the DB file mid-write — a surviving second connection across the swap
    // corrupts the WAL-index ("database disk image is malformed"). MarkAsInitialized only signals
    // the migration gate; deferred maintenance keeps writing afterwards.
    private static TaskCompletionSource<bool> _deferredMaintenanceTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // In-process flag the Android widget checks before opening the DB (WidgetDataService uses its
    // own non-DI connection that ClearAllPools can't reach), so a refresh can't race a restore swap.
    private static volatile bool _restoreInProgress;

    private static bool _isInitialized;
    private static bool _initializationFailed;
    private static Exception? _initializationException;
    private static readonly object _lock = new();

    /// <summary>
    /// In-memory buffer capturing DB init timings and errors. Exposed via
    /// <c>IMigrationService.GetMigrationLog()</c> for sharing from Settings. Static so the
    /// Infrastructure layer (DbInitializer) can reach it without a new abstraction.
    /// </summary>
    public static System.Text.StringBuilder InitLog { get; } = new();

    /// <summary>Appends a timestamped line to <see cref="InitLog"/>. Thread-safe.</summary>
    public static void AppendInitLog(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        lock (InitLog)
        {
            InitLog.AppendLine(line);
        }
        System.Diagnostics.Debug.WriteLine(line);
    }

    /// <summary>True once the database has been initialized.</summary>
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
    /// True after <see cref="MarkAsFailed"/> until cleared via <see cref="ResetForRetry"/>.
    /// Drives whether the UI offers a retry path.
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

    /// <summary>Exception from the most recent <see cref="MarkAsFailed"/>, or null when no failure is recorded.</summary>
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

    /// <summary>Waits for database initialization using <see cref="DefaultTimeout"/>.</summary>
    public static Task EnsureInitializedAsync()
    {
        return EnsureInitializedAsync(DefaultTimeout, CancellationToken.None);
    }

    /// <summary>
    /// Waits for database initialization. Throws <see cref="TimeoutException"/> past
    /// <paramref name="timeout"/>, <see cref="OperationCanceledException"/> on cancellation,
    /// or the stored exception if <see cref="MarkAsFailed"/> was called.
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
    /// True while a backup restore is swapping the DB file. The Android widget checks this
    /// before opening its own connection so a refresh cannot race the swap.
    /// </summary>
    public static bool IsRestoreInProgress => _restoreInProgress;

    /// <summary>Marks the start of a restore (blocks widget DB access).</summary>
    public static void BeginRestore() => _restoreInProgress = true;

    /// <summary>Marks the end of a restore (re-enables widget DB access). Idempotent.</summary>
    public static void EndRestore() => _restoreInProgress = false;

    /// <summary>
    /// Signals that DbInitializer's deferred background maintenance has finished
    /// (called by the Infrastructure layer). Idempotent.
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
    /// Best-effort wait for deferred startup maintenance. Unlike
    /// <see cref="EnsureInitializedAsync()"/> it NEVER throws on timeout — the caller proceeds
    /// anyway (other restore safeguards apply); blocking indefinitely would be worse UX.
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
    /// Marks the database as successfully initialized (called by DbInitializer). Idempotent.
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
    /// Marks the database initialization as failed (called by DbInitializer). Idempotent —
    /// a call after MarkAsInitialized or a previous MarkAsFailed is a no-op.
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
    /// so the caller can re-run init. No-op once initialization has succeeded.
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
    /// Unconditional reset for tests so each starts clean regardless of prior
    /// <see cref="MarkAsInitialized"/> calls.
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
