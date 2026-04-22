using BookLoggerApp.Core.Entitlements;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Singleton that controls paywall visibility across the Blazor UI. A single
/// <see cref="PaywallModal"/> is mounted in <c>MainLayout.razor</c> and listens
/// to <see cref="VisibilityChanged"/>.
/// </summary>
public interface IPaywallCoordinator
{
    event EventHandler? VisibilityChanged;

    bool IsVisible { get; }

    /// <summary>Feature that triggered the paywall open — used for contextual headline. Null when opened from Settings.</summary>
    FeatureKey? TriggerFeature { get; }

    Task ShowAsync(FeatureKey? trigger = null);

    Task HideAsync();
}
