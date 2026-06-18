using BookLoggerApp.Core.Helpers;
using FluentAssertions;

using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class ScannerTaskCompletionHelperTests
{
    [Fact]
    public async Task TrySetCancelledResult_ShouldCompleteWithNull_WhenDismissedWithoutCancelButton()
    {
        var taskCompletionSource = new TaskCompletionSource<string?>();

        ScannerTaskCompletionHelper.TrySetCancelledResult(taskCompletionSource);
        var result = await taskCompletionSource.Task;

        result.Should().BeNull();
    }

    [Fact]
    public async Task TrySetCancelledResult_ShouldNotOverrideExistingScanResult()
    {
        var taskCompletionSource = new TaskCompletionSource<string?>();
        taskCompletionSource.TrySetResult("9781234567890");

        ScannerTaskCompletionHelper.TrySetCancelledResult(taskCompletionSource);
        var result = await taskCompletionSource.Task;

        result.Should().Be("9781234567890");
    }
}
