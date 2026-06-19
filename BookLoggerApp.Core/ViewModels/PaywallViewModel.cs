using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// ViewModel backing the paywall modal. Holds the currently-selected tier,
/// orchestrates the purchase/restore/promo flows, and exposes state so the
/// Blazor components can render without knowing about billing internals.
/// </summary>
public partial class PaywallViewModel : ViewModelBase
{
    private readonly IEntitlementService _entitlementService;
    private readonly IPaywallCoordinator _coordinator;
    private readonly IPromoCodeService _promoCodeService;
    private readonly IBillingService _billingService;
    private readonly IProductCatalog _productCatalog;
    private readonly IAnalyticsService _analytics;

    public PaywallViewModel(
        IEntitlementService entitlementService,
        IPaywallCoordinator coordinator,
        IPromoCodeService promoCodeService,
        IBillingService billingService,
        IProductCatalog productCatalog,
        IAnalyticsService? analytics = null)
    {
        _entitlementService = entitlementService;
        _coordinator = coordinator;
        _promoCodeService = promoCodeService;
        _billingService = billingService;
        _productCatalog = productCatalog;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
    }

    /// Billing period toggle controlling the price shown on each tier card.
    /// Defaults to Yearly (higher conversion and retention than Monthly).
    [ObservableProperty]
    private BillingPeriod _selectedPeriod = BillingPeriod.Yearly;

    [ObservableProperty]
    private string _promoCodeInput = string.Empty;

    [ObservableProperty]
    private string? _banner;

    [ObservableProperty]
    private bool _isPurchaseInProgress;

    [ObservableProperty]
    private bool _showCelebration;

    [ObservableProperty]
    private string _celebrationHeadline = string.Empty;

    [ObservableProperty]
    private string? _celebrationDetail;

    public SubscriptionTier CurrentTier => _entitlementService.CurrentTier;

    public bool IsTierUnlocked(SubscriptionTier tier) => _entitlementService.CurrentTier >= tier;

    /// <summary>
    /// True only when the SKU for <paramref name="tier"/>+<paramref name="period"/> has a configured
    /// introductory offer — drives the paywall "first month" badge (CODE_REVIEW LOG-07).
    /// </summary>
    public bool HasIntroOffer(SubscriptionTier tier, BillingPeriod period)
        => _productCatalog.HasIntroductoryOffer(tier, period);

    public void SelectPeriod(BillingPeriod period)
    {
        SelectedPeriod = period;
    }

    public async Task PurchaseTierAsync(SubscriptionTier tier, BillingPeriod period)
    {
        // BUG-18: reentrancy guard. A double-tap on the buy button (before the UI processes the
        // in-progress flag) must not launch two purchase flows or fire duplicate analytics.
        // Set the flag synchronously before the first await so a near-simultaneous second call
        // short-circuits here, and reset it in finally — not after ExecuteSafelyAsync — so a throw
        // from the wrapper itself can't strand IsPurchaseInProgress=true. Mirrors the entry guard
        // in AppStartupViewModel.StartFlexibleUpdateAsync.
        if (IsPurchaseInProgress)
        {
            return;
        }

        IsPurchaseInProgress = true;
        try
        {
            await ExecuteSafelyAsync(async () =>
            {
                Banner = null;

                string? productId = _productCatalog.GetProductId(tier, period);
                if (productId is null)
                {
                    Banner = $"{tier} is not available as {period}.";
                    return;
                }

                _analytics.LogEvent(AnalyticsEventNames.PurchaseInitiated, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.Tier, tier.ToString())
                    .Add(AnalyticsParamNames.Period, period.ToString())
                    .BuildMutable());

                if (!_billingService.IsConnected)
                {
                    await _billingService.ConnectAsync();
                }

                // BUG-12: when the user already owns a subscription and is switching to a different
                // subscription, hand Play the owned purchase token so it does a proration replacement
                // instead of throwing AlreadyOwned. A managed Lifetime product cannot replace a sub.
                string? oldPurchaseToken = null;
                if (period != BillingPeriod.Lifetime
                    && _entitlementService.CurrentTier != SubscriptionTier.Free
                    && _entitlementService.CurrentEntitlement is { } current
                    && current.BillingPeriod != BillingPeriod.Lifetime
                    && !string.IsNullOrEmpty(current.PurchaseToken))
                {
                    oldPurchaseToken = current.PurchaseToken;
                }

                BillingPurchaseOutcome outcome = await _billingService.LaunchPurchaseFlowAsync(productId, oldPurchaseToken);

                string eventName = outcome switch
                {
                    BillingPurchaseOutcome.Success => AnalyticsEventNames.PurchaseCompleted,
                    BillingPurchaseOutcome.UserCancelled => AnalyticsEventNames.PurchaseCancelled,
                    _ => AnalyticsEventNames.PurchaseFailed
                };

                _analytics.LogEvent(eventName, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.Tier, tier.ToString())
                    .Add(AnalyticsParamNames.Period, period.ToString())
                    .Add(AnalyticsParamNames.Outcome, outcome.ToString())
                    .BuildMutable());

                if (outcome == BillingPurchaseOutcome.Success)
                {
                    CelebrationHeadline = $"{tier} unlocked!";
                    CelebrationDetail = period == BillingPeriod.Lifetime
                        ? "Thanks for going Lifetime — enjoy forever."
                        : "Thanks! Your subscription is active.";
                    ShowCelebration = true;
                    Banner = null;
                }
                else
                {
                    Banner = outcome switch
                    {
                        BillingPurchaseOutcome.UserCancelled => null,
                        BillingPurchaseOutcome.AlreadyOwned => "You already own this subscription.",
                        BillingPurchaseOutcome.BillingUnavailable => "Google Play Billing is not available right now.",
                        BillingPurchaseOutcome.NotAvailable => "This product is not available in your region.",
                        _ => "Purchase failed. Please try again."
                    };
                }
            }, "Failed to start purchase");
        }
        finally
        {
            IsPurchaseInProgress = false;
        }
    }

    [RelayCommand]
    public async Task RestoreAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            Banner = null;
            await _entitlementService.RefreshAsync();
            _analytics.LogEvent(AnalyticsEventNames.PurchaseRestored, AnalyticsParamBuilder.Create()
                .Add(AnalyticsParamNames.Tier, _entitlementService.CurrentTier.ToString())
                .BuildMutable());
            Banner = $"Current tier: {_entitlementService.CurrentTier}.";
        }, Tr("Error_FailedTo_RestorePurchases"));
    }

    [RelayCommand]
    public async Task RedeemPromoAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            PromoCodeRedemptionResult result = await _promoCodeService.RedeemAsync(PromoCodeInput);

            if (result.Success)
            {
                _analytics.LogEvent(AnalyticsEventNames.PromoCodeRedeemed, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.CodeType, "hardcoded")
                    .Add(AnalyticsParamNames.GrantedTier, result.Activation?.GrantedTier.ToString() ?? "unknown")
                    .BuildMutable());
                PromoCodeInput = string.Empty;
                CelebrationHeadline = "Successfully redeemed code";
                CelebrationDetail = result.Message;
                ShowCelebration = true;
                Banner = null;
            }
            else
            {
                Banner = result.Message;
                _analytics.LogEvent(AnalyticsEventNames.PromoCodeFailed, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.Reason, result.Message)
                    .BuildMutable());
            }
        }, Tr("Error_FailedTo_RedeemPromoCode"));
    }

    [RelayCommand]
    public async Task DismissAsync()
    {
        await _coordinator.HideAsync();
    }

    public void DismissCelebration()
    {
        ShowCelebration = false;
    }
}
