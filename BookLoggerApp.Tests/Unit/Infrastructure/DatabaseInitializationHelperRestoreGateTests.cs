using BookLoggerApp.Core.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Infrastructure;

/// <summary>
/// Gates for the backup-restore corruption fix: restore suppresses the widget connection
/// (IsRestoreInProgress) and waits for deferred maintenance before swapping the DB file.
/// </summary>
public class DatabaseInitializationHelperRestoreGateTests : IDisposable
{
    // Leave the shared static gate "fully initialized" for the next serial test.
    public void Dispose()
    {
        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.MarkAsInitialized();
        DatabaseInitializationHelper.MarkDeferredMaintenanceComplete();
    }

    [Fact]
    public void BeginRestore_SetsFlag_AndEndRestore_ClearsIt()
    {
        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.IsRestoreInProgress.Should().BeFalse();

        DatabaseInitializationHelper.BeginRestore();
        DatabaseInitializationHelper.IsRestoreInProgress.Should().BeTrue();

        DatabaseInitializationHelper.EndRestore();
        DatabaseInitializationHelper.IsRestoreInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureDeferredMaintenanceCompleteAsync_ReturnsImmediately_WhenAlreadySignalled()
    {
        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.MarkDeferredMaintenanceComplete();

        // Already signalled, so the timeout is irrelevant; must return instantly.
        var task = DatabaseInitializationHelper.EnsureDeferredMaintenanceCompleteAsync(TimeSpan.FromSeconds(30));
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));

        finished.Should().BeSameAs(task);
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureDeferredMaintenanceCompleteAsync_ProceedsWithoutThrowing_OnTimeout()
    {
        DatabaseInitializationHelper.ResetForTests(); // gate is un-signalled

        // Must not throw on timeout: restore proceeds rather than blocking forever.
        Func<Task> act = () => DatabaseInitializationHelper.EnsureDeferredMaintenanceCompleteAsync(
            TimeSpan.FromMilliseconds(100));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureDeferredMaintenanceCompleteAsync_Completes_WhenSignalledWhileWaiting()
    {
        DatabaseInitializationHelper.ResetForTests();

        var waitTask = DatabaseInitializationHelper.EnsureDeferredMaintenanceCompleteAsync(TimeSpan.FromSeconds(30));
        waitTask.IsCompleted.Should().BeFalse();

        DatabaseInitializationHelper.MarkDeferredMaintenanceComplete();

        var finished = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().BeSameAs(waitTask);
        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }
}
