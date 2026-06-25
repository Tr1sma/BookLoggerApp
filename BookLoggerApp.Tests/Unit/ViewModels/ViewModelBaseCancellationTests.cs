using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

/// <summary>
/// ViewModelBase owns a per-load CancellationTokenSource: a new load supersedes (cancels) the
/// previous one and <see cref="ViewModelBase.CancelOngoingLoad"/> cancels on teardown. A
/// superseded/cancelled load must NOT surface as an error.
/// </summary>
public class ViewModelBaseCancellationTests
{
    private sealed class ScopedVm : ViewModelBase
    {
        public Task RunAsync(Func<CancellationToken, Task> body, string? prefix = null)
            => ExecuteSafelyAsync(body, prefix);

        public Task RunDbAsync(Func<CancellationToken, Task> body, string? prefix = null)
            => ExecuteSafelyWithDbAsync(body, prefix);
    }

    [Fact]
    public async Task ExecuteSafelyAsync_TokenOverload_PassesCancellableTokenAndNoError()
    {
        var vm = new ScopedVm();
        CancellationToken seen = default;

        await vm.RunAsync(ct => { seen = ct; return Task.CompletedTask; });

        seen.CanBeCanceled.Should().BeTrue("the VM must produce a real, cancellable token");
        vm.ErrorMessage.Should().BeNull();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteSafelyWithDbAsync_TokenOverload_PassesCancellableToken()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        var vm = new ScopedVm();
        CancellationToken seen = default;

        await vm.RunDbAsync(ct => { seen = ct; return Task.CompletedTask; });

        seen.CanBeCanceled.Should().BeTrue();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SecondLoad_SupersedesAndCancelsFirst_WithoutError()
    {
        var vm = new ScopedVm();
        var firstStarted = new TaskCompletionSource();
        bool firstObservedCancellation = false;

        var first = vm.RunAsync(async ct =>
        {
            firstStarted.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                firstObservedCancellation = true;
                throw;
            }
        });

        await firstStarted.Task;

        // Starting a second load must cancel the first load's token.
        await vm.RunAsync(_ => Task.CompletedTask);
        await first;

        firstObservedCancellation.Should().BeTrue("a new load must cancel the previous one's token");
        vm.ErrorMessage.Should().BeNull("a superseded load is not an error");
    }

    [Fact]
    public async Task CancelOngoingLoad_CancelsInFlightLoad_WithoutError()
    {
        var vm = new ScopedVm();
        var started = new TaskCompletionSource();
        bool observedCancellation = false;

        var run = vm.RunAsync(async ct =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                observedCancellation = true;
                throw;
            }
        });

        await started.Task;
        vm.CancelOngoingLoad();
        await run;

        observedCancellation.Should().BeTrue();
        vm.ErrorMessage.Should().BeNull("teardown cancellation must not surface as an error");
    }

    [Fact]
    public async Task TokenOverload_NonCancellationException_StillSetsError()
    {
        var vm = new ScopedVm();

        await vm.RunAsync(_ => throw new InvalidOperationException("boom"), "Prefix");

        vm.ErrorMessage.Should().NotBeNull("genuine failures must still surface");
        vm.ErrorMessage.Should().Contain("boom");
    }

    [Fact]
    public async Task SupersededLoad_ThrowingNonCancellationException_DoesNotClobberActiveLoad()
    {
        // A superseded load failing with a non-OCE (e.g. EF "second operation" on the shared
        // DbContext) must not clobber the error/busy state of the newer load that owns the scope.
        var vm = new ScopedVm();
        var firstStarted = new TaskCompletionSource();
        var releaseFirst = new TaskCompletionSource();

        var first = vm.RunAsync(async _ =>
        {
            firstStarted.SetResult();
            await releaseFirst.Task; // held until superseded
            throw new InvalidOperationException("A second operation was started on this context");
        });
        await firstStarted.Task;

        var secondStarted = new TaskCompletionSource();
        var holdSecond = new TaskCompletionSource();
        var second = vm.RunAsync(async _ =>
        {
            secondStarted.SetResult();
            await holdSecond.Task;
        });
        await secondStarted.Task; // second now owns the scope

        releaseFirst.SetResult(); // first (superseded) throws its non-OCE
        await first;

        vm.ErrorMessage.Should().BeNull("a superseded load's failure must not clobber the active load's state");
        vm.IsBusy.Should().BeTrue("the still-running second load owns IsBusy");

        holdSecond.SetResult();
        await second;
        vm.IsBusy.Should().BeFalse();
    }
}
