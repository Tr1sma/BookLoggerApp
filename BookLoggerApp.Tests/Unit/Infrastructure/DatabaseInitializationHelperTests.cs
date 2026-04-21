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
}
