using BookLoggerApp.Core.Helpers;
using FluentAssertions;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class ScannerTaskCompletionHelperTests
{
    [Fact]
    public async Task TrySetCancelledResult_ShouldCompleteWithNull_WhenDismissedWithoutCancelButton()
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource<string?>();

        // Act
        ScannerTaskCompletionHelper.TrySetCancelledResult(taskCompletionSource);
        var result = await taskCompletionSource.Task;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TrySetCancelledResult_ShouldNotOverrideExistingScanResult()
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource<string?>();
        taskCompletionSource.TrySetResult("9781234567890");

        // Act
        ScannerTaskCompletionHelper.TrySetCancelledResult(taskCompletionSource);
        var result = await taskCompletionSource.Task;

        // Assert
        result.Should().Be("9781234567890");
    }
}
