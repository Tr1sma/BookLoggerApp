using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>Backs the paywall modal: holds the selected tier and orchestrates purchase/restore/promo flows.</summary>
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

    /// <summary>Billing period toggle for prices; defaults to Yearly (higher conversion).</summary>
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

    /// <summary>True when the SKU for <paramref name="tier"/>+<paramref name="period"/> has an introductory offer (drives the "first month" badge).</summary>
    public bool HasIntroOffer(SubscriptionTier tier, BillingPeriod period)
        => _productCatalog.HasIntroductoryOffer(tier, period);

    public void SelectPeriod(BillingPeriod period)
    {
        SelectedPeriod = period;
    }

    public async Task PurchaseTierAsync(SubscriptionTier tier, BillingPeriod period)
    {
        // Reentrancy guard: a double-tap must not launch two purchase flows. Flag set before the first await and reset in finally so a throw can't strand it true.
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
                    Banner = Tr("Paywall_TierNotAvailableAsPeriod", tier, period);
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

                // When switching between subscriptions, pass the owned token so Play does a proration replacement instead of throwing AlreadyOwned. Lifetime cannot replace a sub.
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
                    CelebrationHeadline = Tr("Paywall_TierUnlocked", tier);
                    CelebrationDetail = period == BillingPeriod.Lifetime
                        ? Tr("Paywall_LifetimeCelebration")
                        : Tr("Paywall_SubscriptionActive");
                    ShowCelebration = true;
                    Banner = null;
                }
                else
                {
                    Banner = outcome switch
                    {
                        BillingPurchaseOutcome.UserCancelled => null,
                        BillingPurchaseOutcome.AlreadyOwned => Tr("Paywall_AlreadyOwned"),
                        BillingPurchaseOutcome.BillingUnavailable => Tr("Paywall_BillingUnavailable"),
                        BillingPurchaseOutcome.NotAvailable => Tr("Paywall_ProductNotAvailableRegion"),
                        _ => Tr("Paywall_PurchaseFailed")
                    };
                }
            }, Tr("Error_FailedTo_StartPurchase"));
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
            Banner = Tr("Paywall_CurrentTier", _entitlementService.CurrentTier);
        }, Tr("Error_FailedTo_RestorePurchases"));
    }

    [RelayCommand]
    public async Task RedeemPromoAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            PromoCodeRedemptionResult result = await _promoCodeService.RedeemAsync(PromoCodeInput);
            string message = Tr(result.MessageKey, result.MessageArgs);

            if (result.Success)
            {
                _analytics.LogEvent(AnalyticsEventNames.PromoCodeRedeemed, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.CodeType, "hardcoded")
                    .Add(AnalyticsParamNames.GrantedTier, result.Activation?.GrantedTier.ToString() ?? "unknown")
                    .BuildMutable());
                PromoCodeInput = string.Empty;
                CelebrationHeadline = Tr("Paywall_PromoRedeemed");
                CelebrationDetail = message;
                ShowCelebration = true;
                Banner = null;
            }
            else
            {
                Banner = message;
                _analytics.LogEvent(AnalyticsEventNames.PromoCodeFailed, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.Reason, result.MessageKey)
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
