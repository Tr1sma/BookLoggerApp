using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class BackButtonServiceTests
{
    private readonly BackButtonService _service = new();

    [Fact]
    public async Task HandleBackAsync_NoHandlers_ReturnsFalse()
    {
        var result = await _service.HandleBackAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public void Register_NullHandler_IsNoOp()
    {
        Action act = () => _service.Register(null!);

        act.Should().NotThrow();
    }

    [Fact]
    public void Unregister_NullHandler_IsNoOp()
    {
        Action act = () => _service.Unregister(null!);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task HandleBackAsync_SingleHandlerReturnsTrue_ReturnsTrue()
    {
        var calls = 0;
        _service.Register(() => { calls++; return Task.FromResult(true); });

        var result = await _service.HandleBackAsync();

        result.Should().BeTrue();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task HandleBackAsync_SingleHandlerReturnsFalse_ReturnsFalse()
    {
        var calls = 0;
        _service.Register(() => { calls++; return Task.FromResult(false); });

        var result = await _service.HandleBackAsync();

        result.Should().BeFalse();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task HandleBackAsync_LifoOrder_LastRegisteredCalledFirst()
    {
        var invocationOrder = new List<int>();
        Func<Task<bool>> first = () => { invocationOrder.Add(1); return Task.FromResult(false); };
        Func<Task<bool>> second = () => { invocationOrder.Add(2); return Task.FromResult(false); };
        Func<Task<bool>> third = () => { invocationOrder.Add(3); return Task.FromResult(false); };

        _service.Register(first);
        _service.Register(second);
        _service.Register(third);

        await _service.HandleBackAsync();

        invocationOrder.Should().ContainInOrder(3, 2, 1);
    }

    [Fact]
    public async Task HandleBackAsync_HandlerReturnsTrue_StopsPropagation()
    {
        var callCount = new List<int>();
        _service.Register(() => { callCount.Add(1); return Task.FromResult(false); });
        _service.Register(() => { callCount.Add(2); return Task.FromResult(true); });
        _service.Register(() => { callCount.Add(3); return Task.FromResult(false); });

        var result = await _service.HandleBackAsync();

        result.Should().BeTrue();
        callCount.Should().ContainInOrder(3, 2);
        callCount.Should().NotContain(1);
    }

    [Fact]
    public async Task HandleBackAsync_HandlerThrows_ContinuesWithNext()
    {
        var callCount = 0;
        _service.Register(() => { callCount++; return Task.FromResult(false); });
        _service.Register(() => throw new InvalidOperationException("boom"));
        _service.Register(() => { callCount++; return Task.FromResult(false); });

        var result = await _service.HandleBackAsync();

        result.Should().BeFalse();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Unregister_RemovesSpecificHandler()
    {
        var firstCalls = 0;
        var secondCalls = 0;
        Func<Task<bool>> first = () => { firstCalls++; return Task.FromResult(false); };
        Func<Task<bool>> second = () => { secondCalls++; return Task.FromResult(false); };

        _service.Register(first);
        _service.Register(second);
        _service.Unregister(first);

        await _service.HandleBackAsync();

        firstCalls.Should().Be(0);
        secondCalls.Should().Be(1);
    }

    [Fact]
    public async Task HandleBackAsync_SnapshotSemantics_UnregisterDuringIterationDoesNotAffectCurrentCall()
    {
        var calls = 0;
        Func<Task<bool>>? laterHandler = null;
        laterHandler = () => { calls++; return Task.FromResult(false); };

        _service.Register(laterHandler);
        _service.Register(() =>
        {
            _service.Unregister(laterHandler);
            return Task.FromResult(false);
        });

        await _service.HandleBackAsync();

        calls.Should().Be(1);
    }
}
