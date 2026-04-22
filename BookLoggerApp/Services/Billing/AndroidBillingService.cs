#if ANDROID
using Plugin.InAppBilling;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services.Billing;

/// <summary>
/// Android implementation of <see cref="IBillingService"/> wrapping
/// <c>Plugin.InAppBilling</c> (Google Play Billing Library v7+).
///
/// <para><b>Connection lifecycle:</b> the underlying plugin holds a single
/// connection to Google Play. We connect lazily, keep the connection open for
/// the app's lifetime, and disconnect on app shutdown. Re-connects are allowed
/// after transient disconnects.</para>
///
/// <para><b>Subscriptions vs one-shot:</b> <c>premium_lifetime</c> is a
/// Managed Product (ItemType.InAppPurchase); every other SKU is a Subscription.
/// Both branches are queried in parallel and merged so the paywall sees the
/// full catalog.</para>
///
/// <para><b>Acknowledgement:</b> Play auto-refunds subscription purchases that
/// aren't acknowledged within 3 days; we always call
/// <see cref="IInAppBilling.FinalizePurchaseAsync"/> inside the purchase flow.</para>
/// </summary>
public class AndroidBillingService : IBillingService
{
    private readonly IProductCatalog _productCatalog;
    private IInAppBilling? _billing;
    private bool _connected;

    public AndroidBillingService(IProductCatalog productCatalog)
    {
        _productCatalog = productCatalog;
    }

    public bool IsConnected => _connected;

    public event EventHandler<PurchaseResult>? PurchaseUpdated;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            _billing ??= CrossInAppBilling.Current;
            _connected = await _billing.ConnectAsync();
            return _connected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidBillingService.ConnectAsync failed: {ex}");
            _connected = false;
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_billing is not null && _connected)
            {
                await _billing.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidBillingService.DisconnectAsync failed: {ex}");
        }
        finally
        {
            _connected = false;
        }
    }

    public async Task<IReadOnlyList<BillingProduct>> QueryProductsAsync(IEnumerable<string> productIds, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct))
        {
            return Array.Empty<BillingProduct>();
        }

        List<string> all = productIds.ToList();
        List<string> subscriptions = all.Where(id => id != ProductCatalog.PremiumLifetime).ToList();
        List<string> managed = all.Where(id => id == ProductCatalog.PremiumLifetime).ToList();

        List<BillingProduct> results = new();

        try
        {
            if (subscriptions.Count > 0)
            {
                IEnumerable<InAppBillingProduct>? subs = await _billing!.GetProductInfoAsync(ItemType.Subscription, subscriptions.ToArray());
                foreach (var p in subs ?? Enumerable.Empty<InAppBillingProduct>())
                {
                    results.Add(MapProduct(p));
                }
            }

            if (managed.Count > 0)
            {
                IEnumerable<InAppBillingProduct>? oneShot = await _billing!.GetProductInfoAsync(ItemType.InAppPurchase, managed.ToArray());
                foreach (var p in oneShot ?? Enumerable.Empty<InAppBillingProduct>())
                {
                    results.Add(MapProduct(p));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidBillingService.QueryProductsAsync failed: {ex}");
        }

        return results;
    }

    public async Task<IReadOnlyList<PurchaseResult>> QueryActivePurchasesAsync(CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct))
        {
            return Array.Empty<PurchaseResult>();
        }

        List<PurchaseResult> results = new();

        try
        {
            IEnumerable<InAppBillingPurchase>? subs = await _billing!.GetPurchasesAsync(ItemType.Subscription);
            foreach (var purchase in subs ?? Enumerable.Empty<InAppBillingPurchase>())
            {
                PurchaseResult? mapped = TryMapPurchase(purchase);
                if (mapped is not null)
                {
                    results.Add(mapped);
                }
            }

            IEnumerable<InAppBillingPurchase>? oneShot = await _billing!.GetPurchasesAsync(ItemType.InAppPurchase);
            foreach (var purchase in oneShot ?? Enumerable.Empty<InAppBillingPurchase>())
            {
                PurchaseResult? mapped = TryMapPurchase(purchase);
                if (mapped is not null)
                {
                    results.Add(mapped);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidBillingService.QueryActivePurchasesAsync failed: {ex}");
        }

        return results;
    }

    public async Task<BillingPurchaseOutcome> LaunchPurchaseFlowAsync(string productId, string? oldPurchaseToken = null, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct))
        {
            return BillingPurchaseOutcome.BillingUnavailable;
        }

        ItemType itemType = productId == ProductCatalog.PremiumLifetime
            ? ItemType.InAppPurchase
            : ItemType.Subscription;

        try
        {
            InAppBillingPurchase? purchase = await _billing!.PurchaseAsync(productId, itemType);
            if (purchase is null)
            {
                return BillingPurchaseOutcome.UserCancelled;
            }

            // Acknowledge so Play doesn't auto-refund the purchase after 3 days.
            try
            {
                await _billing.FinalizePurchaseAsync(new[] { purchase.TransactionIdentifier ?? purchase.PurchaseToken });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FinalizePurchaseAsync warning: {ex}");
            }

            PurchaseResult? mapped = TryMapPurchase(purchase);
            if (mapped is not null)
            {
                PurchaseUpdated?.Invoke(this, mapped);
            }

            return BillingPurchaseOutcome.Success;
        }
        catch (InAppBillingPurchaseException purchaseEx)
        {
            return purchaseEx.PurchaseError switch
            {
                PurchaseError.UserCancelled => BillingPurchaseOutcome.UserCancelled,
                PurchaseError.AlreadyOwned => BillingPurchaseOutcome.AlreadyOwned,
                PurchaseError.ItemUnavailable => BillingPurchaseOutcome.NotAvailable,
                PurchaseError.BillingUnavailable => BillingPurchaseOutcome.BillingUnavailable,
                PurchaseError.ServiceUnavailable => BillingPurchaseOutcome.BillingUnavailable,
                _ => BillingPurchaseOutcome.Error
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidBillingService.LaunchPurchaseFlowAsync failed: {ex}");
            return BillingPurchaseOutcome.Error;
        }
    }

    public async Task AcknowledgePurchaseAsync(string purchaseToken, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct))
        {
            return;
        }

        try
        {
            await _billing!.FinalizePurchaseAsync(new[] { purchaseToken });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AndroidBillingService.AcknowledgePurchaseAsync failed: {ex}");
        }
    }

    public async Task<bool> LaunchRedeemPromoFlowAsync(CancellationToken ct = default)
    {
        try
        {
            // Google Play supports in-app redemption via the redeem URL; the Play Store
            // app intercepts it and opens its redemption screen.
            return await Microsoft.Maui.ApplicationModel.Launcher.OpenAsync(new Uri("https://play.google.com/redeem"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LaunchRedeemPromoFlowAsync failed: {ex}");
            return false;
        }
    }

    public async Task<bool> OpenSubscriptionManagementAsync(string? productId = null, CancellationToken ct = default)
    {
        try
        {
            string packageName = Android.App.Application.Context.PackageName ?? string.Empty;
            string url = string.IsNullOrEmpty(productId)
                ? $"https://play.google.com/store/account/subscriptions?package={packageName}"
                : $"https://play.google.com/store/account/subscriptions?sku={productId}&package={packageName}";
            return await Microsoft.Maui.ApplicationModel.Launcher.OpenAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenSubscriptionManagementAsync failed: {ex}");
            return false;
        }
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_connected && _billing is not null)
        {
            return true;
        }
        return await ConnectAsync(ct);
    }

    private BillingProduct MapProduct(InAppBillingProduct source)
    {
        (SubscriptionTier Tier, BillingPeriod Period)? resolved = _productCatalog.TryResolve(source.ProductId);
        SubscriptionTier tier = resolved?.Tier ?? SubscriptionTier.Free;
        BillingPeriod period = resolved?.Period ?? BillingPeriod.Monthly;

        return new BillingProduct(
            ProductId: source.ProductId,
            Title: source.LocalizedTitle ?? source.Name ?? source.ProductId,
            Description: source.Description ?? string.Empty,
            FormattedPrice: source.LocalizedPrice ?? string.Empty,
            Tier: tier,
            Period: period,
            HasIntroOffer: false,
            IntroFormattedPrice: null);
    }

    private PurchaseResult? TryMapPurchase(InAppBillingPurchase source)
    {
        (SubscriptionTier Tier, BillingPeriod Period)? resolved = _productCatalog.TryResolve(source.ProductId);
        if (resolved is null)
        {
            return null;
        }

        DateTime? expiresAt = resolved.Value.Period == BillingPeriod.Lifetime
            ? null
            : InferExpiryDate(resolved.Value.Period, source);

        return new PurchaseResult(
            Tier: resolved.Value.Tier,
            Period: resolved.Value.Period,
            ProductId: source.ProductId,
            PurchaseToken: source.PurchaseToken ?? string.Empty,
            OrderId: source.Id,
            PurchasedAt: source.TransactionDateUtc,
            ExpiresAt: expiresAt,
            AutoRenewing: source.AutoRenewing,
            IsInIntroductoryPrice: false,
            IsFamilyShared: false);
    }

    private static DateTime? InferExpiryDate(BillingPeriod period, InAppBillingPurchase purchase)
    {
        // Play Billing exposes the authoritative expiry on the subscription response
        // but the plugin's InAppBillingPurchase does not always surface it as a field.
        // Estimate from the transaction date until the server-verification layer lands.
        DateTime baseDate = purchase.TransactionDateUtc;
        return period switch
        {
            BillingPeriod.Monthly => baseDate.AddDays(30),
            BillingPeriod.Yearly => baseDate.AddDays(365),
            _ => null
        };
    }
}
#endif
