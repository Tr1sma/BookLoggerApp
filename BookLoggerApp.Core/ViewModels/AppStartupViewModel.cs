using CommunityToolkit.Mvvm.ComponentModel;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;

namespace BookLoggerApp.Core.ViewModels;

public partial class AppStartupViewModel : ViewModelBase
{
    private readonly IAppVersionService _appVersionService;
    private readonly IChangelogService _changelogService;
    private readonly IAppUpdateService _appUpdateService;
    private readonly IOnboardingService _onboardingService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IEntitlementService? _entitlementService;
    private readonly IBillingService? _billingService;
    private readonly UserPropertiesPublisher? _userPropertiesPublisher;
    private bool _billingEventHookInstalled;
    private bool _initialized;
    private bool _dismissedUpdateAvailableThisSession;
    private bool _dismissedDownloadedUpdateThisSession;
    private bool _pendingUpdateAvailablePrompt;
    private bool _pendingDownloadedUpdatePrompt;
    private bool _showChangelogAfterOnboarding;

    public AppStartupViewModel(
        IAppVersionService appVersionService,
        IChangelogService changelogService,
        IAppUpdateService appUpdateService,
        IOnboardingService onboardingService,
        IAppSettingsProvider settingsProvider,
        IEntitlementService? entitlementService = null,
        IBillingService? billingService = null,
        UserPropertiesPublisher? userPropertiesPublisher = null)
    {
        _appVersionService = appVersionService;
        _changelogService = changelogService;
        _appUpdateService = appUpdateService;
        _onboardingService = onboardingService;
        _settingsProvider = settingsProvider;
        _entitlementService = entitlementService;
        _billingService = billingService;
        _userPropertiesPublisher = userPropertiesPublisher;
        _appUpdateService.StateChanged += OnAppUpdateStateChanged;
        _onboardingService.StateChanged += OnOnboardingStateChanged;
    }

    [ObservableProperty]
    private bool _isChangelogVisible;

    [ObservableProperty]
    private bool _isUpdateAvailableVisible;

    [ObservableProperty]
    private bool _isUpdateReadyVisible;

    [ObservableProperty]
    private bool _isOnboardingVisible;

    [ObservableProperty]
    private bool _isOnboardingSkipConfirmationVisible;

    [ObservableProperty]
    private bool _showFullHistory;

    [ObservableProperty]
    private bool _isStartingUpdate;

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private ChangelogRelease? _currentRelease;

    [ObservableProperty]
    private IReadOnlyList<ChangelogRelease> _releaseHistory = Array.Empty<ChangelogRelease>();

    [ObservableProperty]
    private AppUpdateState _updateState = AppUpdateState.Unsupported;

    [ObservableProperty]
    private int _onboardingCurrentStep;

    [ObservableProperty]
    private int _onboardingStepCount = OnboardingMissionCatalog.IntroStepCount;

    [ObservableProperty]
    private bool _isPrivacyBannerVisible;

    public bool HasVisibleOverlay => IsOnboardingVisible || IsChangelogVisible || IsUpdateAvailableVisible || IsUpdateReadyVisible;

    public async Task DismissPrivacyBannerAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            if (settings.PrivacyBannerDismissed) return;
            settings.PrivacyBannerDismissed = true;
            settings.PrivacyPolicyAcceptedAt = DateTime.UtcNow;
            await _settingsProvider.UpdateSettingsAsync(settings, ct);
            IsPrivacyBannerVisible = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DismissPrivacyBannerAsync failed: {ex}");
        }
    }

    private async Task RefreshPrivacyBannerVisibilityAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            IsPrivacyBannerVisible = !settings.PrivacyBannerDismissed
                                     && settings.HasCompletedOnboarding
                                     && !IsOnboardingVisible;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshPrivacyBannerVisibility failed: {ex}");
        }
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await ExecuteSafelyAsync(async () =>
        {
            await DatabaseInitializationHelper.EnsureInitializedAsync();

            // Load entitlement tier before any UI renders so HasAccess is correct on first paint.
            if (_entitlementService is not null)
            {
                try
                {
                    await _entitlementService.InitializeAsync(ct);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"EntitlementService.InitializeAsync failed: {ex}");
                }
            }

            // Connect to Play Billing and restore active purchases. Best-effort: must never block startup.
            if (_billingService is not null && _entitlementService is not null)
            {
                try
                {
                    if (!_billingEventHookInstalled)
                    {
                        _billingService.PurchaseUpdated += OnBillingPurchaseUpdated;
                        _billingEventHookInstalled = true;
                    }

                    if (!_billingService.IsConnected)
                    {
                        await _billingService.ConnectAsync(ct);
                    }

                    if (_billingService.IsConnected)
                    {
                        foreach (var active in await _billingService.QueryActivePurchasesAsync(ct))
                        {
                            await _entitlementService.ApplyPurchaseAsync(active, Core.Entitlements.EntitlementChangeReason.Restore, ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Billing restore at startup failed: {ex}");
                }
            }

            _appVersionService.TrackCurrentVersion();
            CurrentVersion = _appVersionService.CurrentVersion;

            var onboardingSnapshot = await _onboardingService.GetSnapshotAsync(ct);

            // Gate on our own persisted "last seen changelog version" flag; MAUI's IsFirstLaunchForCurrentVersion fires on every cold start on some devices.
            var lastSeenChangelog = _appVersionService.LastSeenChangelogVersion;
            var hasUnseenChangelog = !string.Equals(lastSeenChangelog, CurrentVersion, StringComparison.OrdinalIgnoreCase);

            ApplyOnboardingSnapshot(onboardingSnapshot);

            if (onboardingSnapshot.ShouldShowIntro)
            {
                // Never show the changelog on top of the onboarding intro.
                _showChangelogAfterOnboarding = false;
            }
            else if (hasUnseenChangelog)
            {
                await LoadChangelogAsync(ct);
            }

            await RefreshUpdateStateAsync(ct);

            try
            {
                if (_userPropertiesPublisher is not null)
                {
                    var settings = await _settingsProvider.GetSettingsAsync(ct);
                    _userPropertiesPublisher.PublishSettingsOnly(settings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserProperties publish failed: {ex}");
            }

            _initialized = true;
        }, Tr("Error_FailedTo_InitializeStartupExperience"));
    }

    public async Task HandleAppResumedAsync(CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct);
            return;
        }

        await ExecuteSafelyAsync(
            async () => await RefreshUpdateStateAsync(ct),
            Tr("Error_FailedTo_RefreshAppUpdateState"));

        // Re-query Play Billing on resume to detect a subscription cancelled while backgrounded. Best-effort.
        if (_billingService is not null && _entitlementService is not null)
        {
            try
            {
                if (!_billingService.IsConnected)
                {
                    await _billingService.ConnectAsync(ct);
                }

                if (_billingService.IsConnected)
                {
                    IReadOnlyList<Core.Entitlements.PurchaseResult> active = await _billingService.QueryActivePurchasesAsync(ct);
                    if (active.Count > 0)
                    {
                        foreach (var p in active)
                        {
                            await _entitlementService.ApplyPurchaseAsync(p, Core.Entitlements.EntitlementChangeReason.Restore, ct);
                        }
                    }
                    else if (ShouldLapseOnResume(_entitlementService.CurrentEntitlement))
                    {
                        // A genuine Play subscription is gone from Play; downgrade to Free.
                        await _entitlementService.ApplyLapseAsync("expired", ct);
                    }
                }

                await _entitlementService.RefreshAsync(ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Entitlement refresh on resume failed: {ex}");
            }
        }
    }

    /// <summary>
    /// Force-lapse on resume only for a real Play purchase Google no longer returns. Promo grants
    /// lack ProductId/PurchaseToken and never appear in QueryActivePurchasesAsync, so they must not
    /// be downgraded while still valid; expired promos go through the expiry path.
    /// </summary>
    private static bool ShouldLapseOnResume(UserEntitlement? entitlement)
    {
        if (entitlement is null)
        {
            return false;
        }

        if (entitlement.Tier == Core.Entitlements.SubscriptionTier.Free)
        {
            return false;
        }

        if (entitlement.BillingPeriod == Core.Entitlements.BillingPeriod.Lifetime)
        {
            return false;
        }

        // Still-valid promo grant: not a Play purchase, won't be returned by Play. Leave it.
        if (entitlement.PromoExpiresAt is { } promoExpires && promoExpires > DateTime.UtcNow)
        {
            return false;
        }

        // Only a genuine Play purchase carries a ProductId / PurchaseToken.
        return !string.IsNullOrEmpty(entitlement.ProductId)
               || !string.IsNullOrEmpty(entitlement.PurchaseToken);
    }

    public Task ToggleHistoryAsync()
    {
        ShowFullHistory = !ShowFullHistory;
        return Task.CompletedTask;
    }

    public async Task CloseChangelogAsync()
    {
        IsChangelogVisible = false;

        // Persist that this version's changelog was seen so it doesn't re-appear on cold start.
        if (!string.IsNullOrWhiteSpace(CurrentVersion))
        {
            _appVersionService.MarkChangelogSeen(CurrentVersion);
        }

        ShowNextQueuedUpdatePrompt();
        await Task.CompletedTask;
    }

    public async Task OpenChangelogAsync(CancellationToken ct = default)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrEmpty(CurrentVersion))
            {
                _appVersionService.TrackCurrentVersion();
                CurrentVersion = _appVersionService.CurrentVersion;
            }

            ShowFullHistory = true;
            await LoadChangelogAsync(ct);

            if (CurrentRelease == null && ReleaseHistory.Count > 0)
            {
                CurrentRelease = ReleaseHistory[0];
                IsChangelogVisible = true;
            }
        }, Tr("Error_FailedTo_OpenChangelog"));
    }

    public async Task DismissUpdateAvailableAsync()
    {
        _dismissedUpdateAvailableThisSession = true;
        _pendingUpdateAvailablePrompt = false;
        IsUpdateAvailableVisible = false;
        await Task.CompletedTask;
    }

    public async Task DismissUpdateReadyAsync()
    {
        _dismissedDownloadedUpdateThisSession = true;
        _pendingDownloadedUpdatePrompt = false;
        IsUpdateReadyVisible = false;
        await Task.CompletedTask;
    }

    public async Task SkipOnboardingAsync(CancellationToken ct = default)
    {
        IsOnboardingSkipConfirmationVisible = false;
        await ExecuteSafelyAsync(async () =>
        {
            ApplyOnboardingSnapshot(await _onboardingService.CompleteIntroAsync(skipped: true, ct));
            await HandleOnboardingDismissedAsync(ct);
        }, Tr("Error_FailedTo_SkipOnboardingIntro"));
        IsOnboardingVisible = false;
    }

    public async Task CompleteOnboardingAsync(CancellationToken ct = default)
    {
        IsOnboardingSkipConfirmationVisible = false;
        await ExecuteSafelyAsync(async () =>
        {
            ApplyOnboardingSnapshot(await _onboardingService.CompleteIntroAsync(skipped: false, ct));
            await HandleOnboardingDismissedAsync(ct);
        }, Tr("Error_FailedTo_CompleteOnboardingIntro"));
        IsOnboardingVisible = false;
    }

    public void DismissOnboarding()
    {
        IsOnboardingVisible = false;
        IsOnboardingSkipConfirmationVisible = false;
    }

    public async Task AdvanceOnboardingAsync(CancellationToken ct = default)
    {
        IsOnboardingSkipConfirmationVisible = false;
        ApplyOnboardingSnapshot(await _onboardingService.AdvanceIntroAsync(ct));
    }

    public async Task RetreatOnboardingAsync(CancellationToken ct = default)
    {
        IsOnboardingSkipConfirmationVisible = false;
        ApplyOnboardingSnapshot(await _onboardingService.RetreatIntroAsync(ct));
    }

    public Task RequestSkipOnboardingAsync()
    {
        IsOnboardingSkipConfirmationVisible = true;
        return Task.CompletedTask;
    }

    public Task CancelSkipOnboardingAsync()
    {
        IsOnboardingSkipConfirmationVisible = false;
        return Task.CompletedTask;
    }

    public async Task StartFlexibleUpdateAsync(CancellationToken ct = default)
    {
        if (IsStartingUpdate)
        {
            return;
        }

        await ExecuteSafelyAsync(async () =>
        {
            IsStartingUpdate = true;

            if (await _appUpdateService.StartFlexibleUpdateAsync(ct))
            {
                IsUpdateAvailableVisible = false;
                _pendingUpdateAvailablePrompt = false;
                _dismissedUpdateAvailableThisSession = true;
            }

            await RefreshUpdateStateAsync(ct);
        }, Tr("Error_FailedTo_StartAppUpdate"));

        IsStartingUpdate = false;
    }

    public async Task CompleteUpdateAsync(CancellationToken ct = default)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (await _appUpdateService.CompleteUpdateAsync(ct))
            {
                IsUpdateReadyVisible = false;
                _pendingDownloadedUpdatePrompt = false;
            }
        }, Tr("Error_FailedTo_InstallAppUpdate"));
    }

    public async Task<bool> HandleBackAsync()
    {
        if (IsOnboardingVisible)
        {
            if (IsOnboardingSkipConfirmationVisible)
            {
                IsOnboardingSkipConfirmationVisible = false;
                return true;
            }

            if (OnboardingCurrentStep > 0)
            {
                await RetreatOnboardingAsync();
                return true;
            }

            IsOnboardingSkipConfirmationVisible = true;
            return true;
        }

        if (IsUpdateReadyVisible)
        {
            await DismissUpdateReadyAsync();
            return true;
        }

        if (IsUpdateAvailableVisible)
        {
            await DismissUpdateAvailableAsync();
            return true;
        }

        if (IsChangelogVisible)
        {
            await CloseChangelogAsync();
            return true;
        }

        return false;
    }

    public override void Dispose()
    {
        _appUpdateService.StateChanged -= OnAppUpdateStateChanged;
        _onboardingService.StateChanged -= OnOnboardingStateChanged;
        if (_billingService is not null && _billingEventHookInstalled)
        {
            _billingService.PurchaseUpdated -= OnBillingPurchaseUpdated;
            _billingEventHookInstalled = false;
        }
        base.Dispose();
    }

    private void OnBillingPurchaseUpdated(object? sender, Core.Entitlements.PurchaseResult purchase)
    {
        if (_entitlementService is null)
        {
            return;
        }

        // Background billing event — detached so it never touches shared IsBusy/ErrorMessage state.
        _ = ExecuteDetachedAsync(
            () => _entitlementService.ApplyPurchaseAsync(purchase, Core.Entitlements.EntitlementChangeReason.Purchase),
            source: "appstartup_billing_purchase");
    }

    private void OnAppUpdateStateChanged(object? sender, AppUpdateState state)
    {
        // Background update-state event — detached so a throw can't crash the raiser or clobber shared IsBusy/ErrorMessage state.
        _ = ExecuteDetachedAsync(() =>
        {
            UpdateState = state;
            ApplyUpdatePromptState(state);
            return Task.CompletedTask;
        }, source: "appstartup_update_state");
    }

    private void OnOnboardingStateChanged(object? sender, EventArgs e)
    {
        // Background onboarding-state event — detached so it never clobbers shared IsBusy/ErrorMessage state.
        _ = ExecuteDetachedAsync(async () =>
        {
            var snapshot = await _onboardingService.RefreshSnapshotAsync();
            var wasVisible = IsOnboardingVisible;

            ApplyOnboardingSnapshot(snapshot);

            if (wasVisible && !snapshot.ShouldShowIntro)
            {
                await HandleOnboardingDismissedAsync();
            }
        }, source: "appstartup_onboarding_state");
    }

    private async Task LoadChangelogAsync(CancellationToken ct)
    {
        ReleaseHistory = await _changelogService.GetReleaseHistoryAsync(ct);
        CurrentRelease = ReleaseHistory.FirstOrDefault(release =>
            string.Equals(release.Version, ChangelogParser.NormalizeVersion(CurrentVersion), StringComparison.OrdinalIgnoreCase));

        if (CurrentRelease == null)
        {
            var unreleased = await _changelogService.GetUnreleasedChangesAsync(ct);
            if (unreleased?.Sections.Count > 0)
            {
                CurrentRelease = new ChangelogRelease
                {
                    Version = ChangelogParser.NormalizeVersion(CurrentVersion),
                    DisplayVersion = CurrentVersion,
                    Sections = unreleased.Sections
                };
            }
        }

        if (CurrentRelease != null)
        {
            IsChangelogVisible = true;
        }
    }

    private async Task RefreshUpdateStateAsync(CancellationToken ct)
    {
        UpdateState = await _appUpdateService.GetStateAsync(ct);
        ApplyUpdatePromptState(UpdateState);
    }

    private void ApplyUpdatePromptState(AppUpdateState state)
    {
        if (!state.IsSupported)
        {
            IsUpdateAvailableVisible = false;
            IsUpdateReadyVisible = false;
            _pendingUpdateAvailablePrompt = false;
            _pendingDownloadedUpdatePrompt = false;
            return;
        }

        if (state.IsUpdateDownloaded)
        {
            _pendingDownloadedUpdatePrompt = !_dismissedDownloadedUpdateThisSession;
            _pendingUpdateAvailablePrompt = false;

            if (IsOnboardingVisible || IsChangelogVisible)
            {
                IsUpdateAvailableVisible = false;
                IsUpdateReadyVisible = false;
                OnPropertyChanged(nameof(HasVisibleOverlay));
                return;
            }

            IsUpdateAvailableVisible = false;
            IsUpdateReadyVisible = _pendingDownloadedUpdatePrompt;
            OnPropertyChanged(nameof(HasVisibleOverlay));
            return;
        }

        IsUpdateReadyVisible = false;
        _pendingDownloadedUpdatePrompt = false;

        if (state.IsUpdateInProgress)
        {
            IsUpdateAvailableVisible = false;
            IsUpdateReadyVisible = false;
            _pendingUpdateAvailablePrompt = false;
            _pendingDownloadedUpdatePrompt = false;
            OnPropertyChanged(nameof(HasVisibleOverlay));
            return;
        }

        if (state.IsUpdateAvailable && !_dismissedUpdateAvailableThisSession)
        {
            _pendingUpdateAvailablePrompt = true;

            if (IsOnboardingVisible || IsChangelogVisible)
            {
                IsUpdateAvailableVisible = false;
                OnPropertyChanged(nameof(HasVisibleOverlay));
                return;
            }

            IsUpdateAvailableVisible = true;
            OnPropertyChanged(nameof(HasVisibleOverlay));
            return;
        }

        _pendingUpdateAvailablePrompt = false;
        IsUpdateAvailableVisible = false;
        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    private void ShowNextQueuedUpdatePrompt()
    {
        if (IsOnboardingVisible || IsChangelogVisible)
        {
            IsUpdateAvailableVisible = false;
            IsUpdateReadyVisible = false;
            OnPropertyChanged(nameof(HasVisibleOverlay));
            return;
        }

        if (_pendingDownloadedUpdatePrompt && !_dismissedDownloadedUpdateThisSession)
        {
            IsUpdateReadyVisible = true;
            IsUpdateAvailableVisible = false;
        }
        else if (_pendingUpdateAvailablePrompt && !_dismissedUpdateAvailableThisSession)
        {
            IsUpdateAvailableVisible = true;
            IsUpdateReadyVisible = false;
        }

        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    partial void OnIsChangelogVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    partial void OnIsUpdateAvailableVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    partial void OnIsUpdateReadyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    partial void OnIsOnboardingVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleOverlay));
        _ = RefreshPrivacyBannerVisibilityAsync();
    }

    private void ApplyOnboardingSnapshot(OnboardingSnapshot snapshot)
    {
        OnboardingCurrentStep = snapshot.CurrentIntroStep;
        OnboardingStepCount = snapshot.IntroStepCount;
        IsOnboardingVisible = snapshot.ShouldShowIntro;

        if (!snapshot.ShouldShowIntro)
        {
            IsOnboardingSkipConfirmationVisible = false;
        }

        _ = RefreshPrivacyBannerVisibilityAsync();
    }

    private async Task HandleOnboardingDismissedAsync(CancellationToken ct = default)
    {
        if (_showChangelogAfterOnboarding)
        {
            _showChangelogAfterOnboarding = false;
            await LoadChangelogAsync(ct);
        }

        ShowNextQueuedUpdatePrompt();
    }
}
