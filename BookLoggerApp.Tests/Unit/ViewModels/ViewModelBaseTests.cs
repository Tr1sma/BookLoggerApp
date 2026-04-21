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
