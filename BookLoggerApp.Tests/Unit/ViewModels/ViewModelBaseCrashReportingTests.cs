using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Services.Analytics;
using BookLoggerApp.Core.ViewModels;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class ViewModelBaseCrashReportingTests : IDisposable
{
    private readonly MockCrashReportingService _mockCrash = new();

    public ViewModelBaseCrashReportingTests()
    {
        DatabaseInitializationHelper.ResetForTests();
        DatabaseInitializationHelper.MarkAsInitialized();
        ViewModelBase.CrashReporter = _mockCrash;
    }

    public void Dispose()
    {
        ViewModelBase.CrashReporter = NoOpCrashReportingService.Instance;
        DatabaseInitializationHelper.ResetForTests();
    }

    [Fact]
    public async Task ExecuteSafelyAsync_reports_non_fatal_on_exception()
    {
        var vm = new TestViewModel();

        await vm.RunExecuteSafelyAsync(() => throw new InvalidOperationException("boom"), "ctx");

        _mockCrash.NonFatals.Should().HaveCount(1);
        _mockCrash.NonFatals[0].Exception.Should().BeOfType<InvalidOperationException>();
        _mockCrash.NonFatals[0].Keys!["source"].Should().Be("viewmodel");
        _mockCrash.NonFatals[0].Keys!["prefix"].Should().Be("ctx");
    }

    [Fact]
    public async Task ExecuteSafelyAsync_does_not_report_when_action_succeeds()
    {
        var vm = new TestViewModel();

        await vm.RunExecuteSafelyAsync(() => Task.CompletedTask, "ok");

        _mockCrash.NonFatals.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteSafelyWithDbAsync_reports_non_fatal_with_db_source()
    {
        var vm = new TestViewModel();

        await vm.RunExecuteSafelyWithDbAsync(() => throw new InvalidOperationException("db boom"), "dbctx");

        _mockCrash.NonFatals.Should().HaveCount(1);
        _mockCrash.NonFatals[0].Keys!["source"].Should().Be("viewmodel_db");
    }

    private sealed partial class TestViewModel : ViewModelBase
    {
        public Task RunExecuteSafelyAsync(Func<Task> action, string prefix)
            => ExecuteSafelyAsync(action, prefix);

        public Task RunExecuteSafelyWithDbAsync(Func<Task> action, string prefix)
            => ExecuteSafelyWithDbAsync(action, prefix);
    }
}
