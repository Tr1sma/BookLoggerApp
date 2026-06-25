using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class ViewModelBaseTests
{
    public ViewModelBaseTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
    }

    private class TestViewModel : ViewModelBase
    {
        public Task RunAsync(Func<Task> action, string? prefix = null)
            => ExecuteSafelyAsync(action, prefix);

        public Task RunWithDbAsync(Func<Task> action, string? prefix = null)
            => ExecuteSafelyWithDbAsync(action, prefix);

        public void CallClearError() => base.GetType(); // placeholder
    }

    [Fact]
    public async Task ExecuteSafelyAsync_SuccessPath_SetsIsBusyFalseAndClearsError()
    {
        var vm = new TestViewModel();
        var ranAction = false;

        await vm.RunAsync(async () =>
        {
            vm.IsBusy.Should().BeTrue();
            ranAction = true;
            await Task.CompletedTask;
        });

        ranAction.Should().BeTrue();
        vm.IsBusy.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteSafelyAsync_Exception_SetsErrorMessageWithPrefix()
    {
        var vm = new TestViewModel();

        await vm.RunAsync(() => throw new InvalidOperationException("oops"), "Custom prefix");

        vm.IsBusy.Should().BeFalse();
        vm.ErrorMessage.Should().NotBeNull();
        vm.ErrorMessage!.Should().StartWith("Custom prefix:");
        vm.ErrorMessage.Should().Contain("oops");
    }

    [Fact]
    public async Task ExecuteSafelyAsync_ExceptionWithoutPrefix_UsesDefault()
    {
        var vm = new TestViewModel();

        await vm.RunAsync(() => throw new InvalidOperationException("boom"));

        vm.ErrorMessage!.Should().StartWith("An error occurred");
    }

    [Fact]
    public async Task ExecuteSafelyWithDbAsync_Success_ExecutesAction()
    {
        var vm = new TestViewModel();
        var ran = false;

        await vm.RunWithDbAsync(async () =>
        {
            ran = true;
            await Task.CompletedTask;
        });

        ran.Should().BeTrue();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteSafelyWithDbAsync_Exception_HandlesError()
    {
        var vm = new TestViewModel();

        await vm.RunWithDbAsync(() => throw new InvalidOperationException("db err"), "DB fail");

        vm.ErrorMessage.Should().Contain("db err");
        vm.ErrorMessage.Should().StartWith("DB fail:");
    }
}

public class ViewModelBaseLoadScopeTests
{
    public ViewModelBaseLoadScopeTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
    }

    private class TestViewModel : ViewModelBase
    {
        public Task RunCtAsync(Func<CancellationToken, Task> action, string? prefix = null)
            => ExecuteSafelyAsync(action, prefix);
    }

    [Fact]
    public async Task ExecuteSafelyAsync_CtOverload_SuccessPath_PassesUncancelledToken()
    {
        var vm = new TestViewModel();
        CancellationToken observed = new(canceled: true);

        await vm.RunCtAsync(ct =>
        {
            observed = ct;
            return Task.CompletedTask;
        });

        observed.IsCancellationRequested.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteSafelyAsync_SecondLoad_CancelsFirstLoadToken()
    {
        var vm = new TestViewModel();
        CancellationToken firstToken = default;
        var firstStarted = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var firstLoad = vm.RunCtAsync(async ct =>
        {
            firstToken = ct;
            firstStarted.SetResult();
            await release.Task;
            ct.ThrowIfCancellationRequested();
        });

        await firstStarted.Task;
        firstToken.IsCancellationRequested.Should().BeFalse();

        // A second load supersedes the first.
        await vm.RunCtAsync(_ => Task.CompletedTask);

        firstToken.IsCancellationRequested.Should().BeTrue();

        release.SetResult();
        await firstLoad;

        // The superseded load must not surface its cancellation as a user-facing error.
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Dispose_CancelsCurrentLoadToken()
    {
        var vm = new TestViewModel();
        CancellationToken token = default;
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var load = vm.RunCtAsync(async ct =>
        {
            token = ct;
            started.SetResult();
            await release.Task;
        });

        await started.Task;
        vm.Dispose();

        token.IsCancellationRequested.Should().BeTrue();

        release.SetResult();
        await load;
    }
}

public class ViewModelBaseTimeoutTests : IDisposable
{
    public ViewModelBaseTimeoutTests()
    {
        DatabaseInitializationHelper.ResetForTests();
    }

    public void Dispose()
    {
        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.MarkAsInitialized();
    }

    private class TestViewModel : ViewModelBase
    {
        public Task RunWithDbAsync(Func<Task> action, string? prefix = null)
            => ExecuteSafelyWithDbAsync(action, prefix);
    }

    [Fact]
    public async Task ExecuteSafelyWithDbAsync_WhenInitFaultedAsTimeout_SetsDbInitErrorMessage()
    {
        var vm = new TestViewModel();
        DatabaseInitializationHelper.MarkAsFailed(new TimeoutException("slow"));

        await vm.RunWithDbAsync(() => Task.CompletedTask, "Fehler beim Laden");

        // Prefix is verbatim; body comes from Error_DbInitializing (English under invariant culture).
        vm.ErrorMessage.Should().NotBeNull();
        vm.ErrorMessage!.Should().StartWith("Fehler beim Laden:");
        vm.ErrorMessage.Should().Contain("Database is still initializing");
    }

    [Fact]
    public async Task ExecuteSafelyWithDbAsync_WhenInitFaultedAsTimeout_ClearsIsBusy()
    {
        var vm = new TestViewModel();
        DatabaseInitializationHelper.MarkAsFailed(new TimeoutException("slow"));

        await vm.RunWithDbAsync(() => Task.CompletedTask);

        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteSafelyWithDbAsync_WhenInitFaultedAsTimeout_DoesNotRunAction()
    {
        var vm = new TestViewModel();
        DatabaseInitializationHelper.MarkAsFailed(new TimeoutException("slow"));
        var ran = false;

        await vm.RunWithDbAsync(() =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        ran.Should().BeFalse();
    }

    [Fact]
    public void IsDatabaseInitializationFailed_ReflectsHelperState()
    {
        var vm = new TestViewModel();
        vm.IsDatabaseInitializationFailed.Should().BeFalse();

        DatabaseInitializationHelper.MarkAsFailed(new InvalidOperationException("x"));
        vm.IsDatabaseInitializationFailed.Should().BeTrue();

        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.MarkAsInitialized();
        vm.IsDatabaseInitializationFailed.Should().BeFalse();
    }
}
