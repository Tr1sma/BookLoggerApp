using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Configurable mock implementation of IAdService for testing.
/// </summary>
public class MockAdService : IAdService
{
    public bool IsBannerVisible { get; set; }
    public bool IsPrivacyOptionsRequired { get; set; }
    public bool ShouldInitializeSuccessfully { get; set; } = true;
    public int InitializeCallCount { get; private set; }
    public int RefreshCallCount { get; private set; }
    public int ShowPrivacyCallCount { get; private set; }

    public event EventHandler<bool>? BannerVisibilityChanged;

    public Task InitializeAsync(CancellationToken ct = default)
    {
        InitializeCallCount++;
        if (!ShouldInitializeSuccessfully)
            throw new InvalidOperationException("Mock initialization failure");
        return Task.CompletedTask;
    }

    public Task RefreshAdVisibilityAsync(CancellationToken ct = default)
    {
        RefreshCallCount++;
        return Task.CompletedTask;
    }

    public Task ShowPrivacyOptionsAsync(CancellationToken ct = default)
    {
        ShowPrivacyCallCount++;
        return Task.CompletedTask;
    }

    public void SimulateBannerVisibilityChange(bool visible)
    {
        IsBannerVisible = visible;
        BannerVisibilityChanged?.Invoke(this, visible);
    }
}
