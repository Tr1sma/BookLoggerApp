using BookLoggerApp.Core.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Infrastructure;

public class DatabaseInitializationHelperTests : IDisposable
{
    public DatabaseInitializationHelperTests()
    {
        DatabaseInitializationHelper.ResetForTests();
    }

    public void Dispose()
    {
        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.MarkAsInitialized();
    }

    [Fact]
    public async Task EnsureInitializedAsync_WithTimeout_ThrowsTimeoutException()
    {
        Func<Task> act = () => DatabaseInitializationHelper.EnsureInitializedAsync(TimeSpan.FromMilliseconds(50));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task EnsureInitializedAsync_MarkedBeforeTimeout_Completes()
    {
        DatabaseInitializationHelper.MarkAsInitialized();

        await DatabaseInitializationHelper.EnsureInitializedAsync(TimeSpan.FromSeconds(1));

        DatabaseInitializationHelper.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureInitializedAsync_AfterMarkAsFailed_RethrowsStoredException()
    {
        InvalidOperationException stored = new("boom");
        DatabaseInitializationHelper.MarkAsFailed(stored);

        Func<Task> act = () => DatabaseInitializationHelper.EnsureInitializedAsync(TimeSpan.FromSeconds(1));

        InvalidOperationException thrown = (await act.Should().ThrowAsync<InvalidOperationException>()).And;
        thrown.Message.Should().Be("boom");
    }

    [Fact]
    public async Task EnsureInitializedAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = () => DatabaseInitializationHelper.EnsureInitializedAsync(TimeSpan.FromSeconds(30), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void MarkAsFailed_SetsInitializationFailedFlag()
    {
        DatabaseInitializationHelper.InitializationFailed.Should().BeFalse();

        DatabaseInitializationHelper.MarkAsFailed(new InvalidOperationException("x"));

        DatabaseInitializationHelper.InitializationFailed.Should().BeTrue();
        DatabaseInitializationHelper.InitializationException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ResetForRetry_AfterFailure_AllowsFreshAwait()
    {
        DatabaseInitializationHelper.MarkAsFailed(new InvalidOperationException("first"));

        DatabaseInitializationHelper.ResetForRetry();
        DatabaseInitializationHelper.InitializationFailed.Should().BeFalse();

        DatabaseInitializationHelper.MarkAsInitialized();
        await DatabaseInitializationHelper.EnsureInitializedAsync(TimeSpan.FromSeconds(1));

        DatabaseInitializationHelper.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void ResetForRetry_AfterSuccess_IsNoOp()
    {
        DatabaseInitializationHelper.MarkAsInitialized();

        DatabaseInitializationHelper.ResetForRetry();

        DatabaseInitializationHelper.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void MarkAsInitialized_IsIdempotent()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        DatabaseInitializationHelper.MarkAsInitialized();

        DatabaseInitializationHelper.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void MarkAsFailed_AfterInitialized_DoesNotOverwriteSuccess()
    {
        DatabaseInitializationHelper.MarkAsInitialized();

        DatabaseInitializationHelper.MarkAsFailed(new InvalidOperationException("late"));

        DatabaseInitializationHelper.IsInitialized.Should().BeTrue();
        DatabaseInitializationHelper.InitializationFailed.Should().BeFalse();
    }

    [Fact]
    public void DefaultTimeout_IsReasonableForBudgetDevices()
    {
        // Keep this assertion tight — a drift back toward the old 45s silently
        // regresses UX on affected devices. The actual value is intentional and
        // informed by field telemetry; change both together.
        DatabaseInitializationHelper.DefaultTimeout.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(20));
        DatabaseInitializationHelper.DefaultTimeout.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task EnsureInitializedAsync_Continuation_RunsAsynchronously()
    {
        // Regression guard for RunContinuationsAsynchronously on the TCS: without
        // that flag, awaiters resume synchronously on whatever thread called
        // MarkAsInitialized, which in production is the DB-init worker. That
        // hijacks the worker and can starve any subsequent init work.
        Task awaitTask = DatabaseInitializationHelper.EnsureInitializedAsync(TimeSpan.FromSeconds(5));

        int settingThreadId = 0;
        int continuationThreadId = 0;

        Task markTask = Task.Run(() =>
        {
            settingThreadId = Environment.CurrentManagedThreadId;
            DatabaseInitializationHelper.MarkAsInitialized();
        });

        Task observeTask = awaitTask.ContinueWith(
            _ => continuationThreadId = Environment.CurrentManagedThreadId,
            TaskContinuationOptions.ExecuteSynchronously);

        await Task.WhenAll(markTask, awaitTask, observeTask);

        settingThreadId.Should().NotBe(0);
        continuationThreadId.Should().NotBe(0);
        continuationThreadId.Should().NotBe(settingThreadId,
            "the awaiter must not resume synchronously on the thread that signalled the TCS");
    }
}
