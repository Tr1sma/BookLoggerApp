using Microsoft.Extensions.Logging;

using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

/// <summary>
/// Singleton service managing AdMob banner ads and GDPR/UMP consent.
/// Uses Plugin.AdMob for ad display and consent handling.
/// </summary>
public class AdService : IAdService, IDisposable
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<AdService>? _logger;
    private bool _isInitialized;
    private bool _isBannerVisible;
    private bool _canRequestAds;
    private bool _isPrivacyOptionsRequired;

    public bool IsBannerVisible => _isBannerVisible;
    public bool IsPrivacyOptionsRequired => _isPrivacyOptionsRequired;

    public event EventHandler<bool>? BannerVisibilityChanged;

    public AdService(
        ISubscriptionService subscriptionService,
        ILogger<AdService>? logger = null)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;

        _subscriptionService.TierChanged += OnTierChanged;
    }

#if ANDROID
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized)
            return;

        try
        {
            _logger?.LogInformation("Initializing AdMob SDK...");

            if (AdConfiguration.UseTestAds)
            {
                Plugin.AdMob.Configuration.AdConfig.UseTestAdUnitIds = true;
            }

            // Plugin.AdMob handles consent automatically via UseAdMob().
            // Check consent status after SDK initialization.
            var consentService = GetConsentService();
            if (consentService != null)
            {
                _canRequestAds = consentService.CanRequestAds();
                _isPrivacyOptionsRequired = consentService.IsPrivacyOptionsRequired();
            }
            else
            {
                _canRequestAds = true;
            }

            _isInitialized = true;
            _logger?.LogInformation(
                "AdMob initialized. CanRequestAds={CanRequest}, PrivacyRequired={Privacy}",
                _canRequestAds, _isPrivacyOptionsRequired);

            await RefreshAdVisibilityAsync(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize AdMob (non-fatal)");
            _isInitialized = true;
        }
    }

    public async Task RefreshAdVisibilityAsync(CancellationToken ct = default)
    {
        try
        {
            bool hasAdFree = await _subscriptionService.HasFeatureAccessAsync(
                FeatureFlag.AdFree, ct);

            bool shouldShow = !hasAdFree && _canRequestAds;
            SetBannerVisibility(shouldShow);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh ad visibility");
        }
    }

    public Task ShowPrivacyOptionsAsync(CancellationToken ct = default)
    {
        try
        {
            var consentService = GetConsentService();
            consentService?.ShowPrivacyOptionsForm();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to show privacy options");
        }
        return Task.CompletedTask;
    }

    private static Plugin.AdMob.Services.IAdConsentService? GetConsentService()
    {
        try
        {
            return IPlatformApplication.Current?.Services
                ?.GetService<Plugin.AdMob.Services.IAdConsentService>();
        }
        catch
        {
            return null;
        }
    }

#else
    public Task InitializeAsync(CancellationToken ct = default)
    {
        _isInitialized = true;
        return Task.CompletedTask;
    }

    public Task RefreshAdVisibilityAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ShowPrivacyOptionsAsync(CancellationToken ct = default)
        => Task.CompletedTask;
#endif

    private void SetBannerVisibility(bool visible)
    {
        if (_isBannerVisible == visible)
            return;

        _isBannerVisible = visible;
        _logger?.LogInformation("Banner visibility changed to {Visible}", visible);
        BannerVisibilityChanged?.Invoke(this, visible);
    }

    private void OnTierChanged(object? sender, EventArgs e)
    {
        _ = RefreshAdVisibilityAsync();
    }

    public void Dispose()
    {
        _subscriptionService.TierChanged -= OnTierChanged;
    }
}
