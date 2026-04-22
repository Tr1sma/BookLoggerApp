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

    [ObservableProperty]
    private SubscriptionTier _selectedTier = SubscriptionTier.Plus;

    [ObservableProperty]
    private string _promoCodeInput = string.Empty;

    [ObservableProperty]
    private string? _banner;

    [ObservableProperty]
    private bool _isPurchaseInProgress;

    public SubscriptionTier CurrentTier => _entitlementService.CurrentTier;

    public bool IsTierUnlocked(SubscriptionTier tier) => _entitlementService.CurrentTier >= tier;

    [RelayCommand]
    public void SelectTier(SubscriptionTier tier)
    {
        SelectedTier = tier;
        _analytics.LogEvent(AnalyticsEventNames.PaywallTierSelected, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.Tier, tier.ToString())
            .BuildMutable());
    }

    [RelayCommand]
    public async Task PurchaseAsync(BillingPeriod period)
    {
        await ExecuteSafelyAsync(async () =>
        {
            IsPurchaseInProgress = true;
            Banner = null;

            string? productId = _productCatalog.GetProductId(SelectedTier, period);
            if (productId is null)
            {
                Banner = $"{SelectedTier} is not available as {period}.";
                return;
            }

            _analytics.LogEvent(AnalyticsEventNames.PurchaseInitiated, AnalyticsParamBuilder.Create()
                .Add(AnalyticsParamNames.Tier, SelectedTier.ToString())
                .Add(AnalyticsParamNames.Period, period.ToString())
                .BuildMutable());

            if (!_billingService.IsConnected)
            {
                await _billingService.ConnectAsync();
            }

            BillingPurchaseOutcome outcome = await _billingService.LaunchPurchaseFlowAsync(productId);

            string eventName = outcome switch
            {
                BillingPurchaseOutcome.Success => AnalyticsEventNames.PurchaseCompleted,
                BillingPurchaseOutcome.UserCancelled => AnalyticsEventNames.PurchaseCancelled,
                _ => AnalyticsEventNames.PurchaseFailed
            };

            _analytics.LogEvent(eventName, AnalyticsParamBuilder.Create()
                .Add(AnalyticsParamNames.Tier, SelectedTier.ToString())
                .Add(AnalyticsParamNames.Period, period.ToString())
                .Add(AnalyticsParamNames.Outcome, outcome.ToString())
                .BuildMutable());

            Banner = outcome switch
            {
                BillingPurchaseOutcome.Success => "Thank you! Your purchase is being processed.",
                BillingPurchaseOutcome.UserCancelled => null,
                BillingPurchaseOutcome.AlreadyOwned => "You already own this subscription.",
                BillingPurchaseOutcome.BillingUnavailable => "Google Play Billing is not available right now.",
                BillingPurchaseOutcome.NotAvailable => "This product is not available in your region.",
                _ => "Purchase failed. Please try again."
            };
        }, "Failed to start purchase");

        IsPurchaseInProgress = false;
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
        }, "Failed to restore purchases");
    }

    [RelayCommand]
    public async Task RedeemPromoAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            PromoCodeRedemptionResult result = await _promoCodeService.RedeemAsync(PromoCodeInput);
            Banner = result.Message;

            if (result.Success)
            {
                _analytics.LogEvent(AnalyticsEventNames.PromoCodeRedeemed, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.CodeType, "hardcoded")
                    .Add(AnalyticsParamNames.GrantedTier, result.Activation?.GrantedTier.ToString() ?? "unknown")
                    .BuildMutable());
                PromoCodeInput = string.Empty;
            }
            else
            {
                _analytics.LogEvent(AnalyticsEventNames.PromoCodeFailed, AnalyticsParamBuilder.Create()
                    .Add(AnalyticsParamNames.Reason, result.Message)
                    .BuildMutable());
            }
        }, "Failed to redeem promo code");
    }

    [RelayCommand]
    public async Task DismissAsync()
    {
        await _coordinator.HideAsync();
    }
}
