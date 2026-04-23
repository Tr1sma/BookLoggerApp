using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookLoggerApp.Infrastructure.Services;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly object _retryLock = new();
    private Thread? _activeRetryThread;

    public DatabaseInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Retry()
    {
        // Idempotent: if a retry is already running, don't spawn another. Multiple
        // concurrent DbInitializer.InitializeAsync calls would race on the same
        // SQLite file and produce unpredictable lock behaviour — especially bad
        // when users tap the retry button repeatedly.
        lock (_retryLock)
        {
            if (_activeRetryThread is { IsAlive: true })
            {
                System.Diagnostics.Debug.WriteLine("DatabaseInitializer.Retry: already running, skipping.");
                DatabaseInitializationHelper.AppendInitLog("Retry requested — a retry is already running, skipping");
                return;
            }

            DatabaseInitializationHelper.AppendInitLog("Retry requested — resetting gate and spawning DbInit-Retry thread");
            DatabaseInitializationHelper.ResetForRetry();

            var thread = new Thread(() =>
            {
                DatabaseInitializationHelper.AppendInitLog(
                    $"DbInit-Retry thread running (managed thread id={Environment.CurrentManagedThreadId})");
                try
                {
                    var logger = _serviceProvider.GetService<ILogger<AppDbContext>>();
                    DbInitializer.InitializeAsync(_serviceProvider, logger).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"=== EXCEPTION IN DATABASE RETRY ===");
                    System.Diagnostics.Debug.WriteLine($"{ex}");
                    System.Diagnostics.Debug.WriteLine("=== END EXCEPTION ===");
                    DatabaseInitializationHelper.AppendInitLog(
                        $"Retry thread caught exception: {ex.GetType().Name}: {ex.Message}");

                    DatabaseInitializationHelper.MarkAsFailed(ex);
                }
            })
            {
                IsBackground = true,
                Name = "DbInit-Retry"
            };
            _activeRetryThread = thread;
            thread.Start();
        }
    }
}
