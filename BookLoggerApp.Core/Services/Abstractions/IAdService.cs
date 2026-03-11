namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Manages ad display and GDPR/UMP consent.
/// Implemented in the MAUI project with platform-specific code.
/// Registered as singleton.
/// </summary>
public interface IAdService
{
    /// <summary>
    /// Whether the ad banner is currently visible.
    /// </summary>
    bool IsBannerVisible { get; }

    /// <summary>
    /// Whether the privacy options entry point is required (EU users).
    /// When true, the Settings page should show a "Privacy Settings" button.
    /// </summary>
    bool IsPrivacyOptionsRequired { get; }

    /// <summary>
    /// Raised when the banner visibility changes (shown/hidden).
    /// Used by the MAUI native layer to show/hide the ad view.
    /// </summary>
    event EventHandler<bool>? BannerVisibilityChanged;

    /// <summary>
    /// Initializes the ad SDK and checks GDPR/UMP consent status.
    /// Should be called once on app startup, after database initialization.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Evaluates whether ads should be shown based on subscription tier
    /// and consent status, then updates banner visibility accordingly.
    /// Called after initialization and when subscription tier changes.
    /// </summary>
    Task RefreshAdVisibilityAsync(CancellationToken ct = default);

    /// <summary>
    /// Shows the GDPR/UMP privacy options form so users can update their consent.
    /// </summary>
    Task ShowPrivacyOptionsAsync(CancellationToken ct = default);
}
