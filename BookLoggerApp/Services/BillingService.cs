using Microsoft.Extensions.Logging;

using BookLoggerApp.Core.Constants;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

/// <summary>
/// Singleton service that handles Google Play Billing via Plugin.InAppBilling.
/// Each method connects → does work → disconnects in a try/finally pattern.
/// </summary>
public class BillingService : IBillingService
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<BillingService>? _logger;

    public BillingService(ISubscriptionService subscriptionService, ILogger<BillingService>? logger = null)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

#if ANDROID
    public async Task<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken ct = default)
    {
        var billing = Plugin.InAppBilling.CrossInAppBilling.Current;
        try
        {
            var connected = await billing.ConnectAsync();
            if (!connected)
            {
                _logger?.LogWarning("Could not connect to billing service");
                return [];
            }

            var products = await billing.GetProductInfoAsync(
                Plugin.InAppBilling.ItemType.Subscription,
                BillingConstants.AllProductIds);

            if (products == null)
                return [];

            return products.Select(p => new ProductInfo
            {
                ProductId = p.ProductId,
                Name = p.Name,
                LocalizedPrice = p.LocalizedPrice,
                PriceAmount = (decimal)p.MicrosPrice / 1_000_000m,
                CurrencyCode = p.CurrencyCode ?? string.Empty,
                Description = p.Description
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get products");
            return [];
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }

    public async Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken ct = default)
    {
        var billing = Plugin.InAppBilling.CrossInAppBilling.Current;
        try
        {
            var connected = await billing.ConnectAsync();
            if (!connected)
                return PurchaseResult.Failed("Could not connect to billing service.");

            var purchase = await billing.PurchaseAsync(
                productId,
                Plugin.InAppBilling.ItemType.Subscription);

            if (purchase == null)
                return PurchaseResult.Cancelled();

            // Google Play requires acknowledgement within 3 days
            if (purchase.IsAcknowledged == false)
            {
                await billing.FinalizePurchaseAsync([purchase.PurchaseToken]);
            }

            var duration = BillingConstants.GetEstimatedDuration(productId);
            var expiresAt = DateTime.UtcNow.Add(duration);

            await _subscriptionService.UpdateTierAsync(
                SubscriptionTier.Premium,
                productId,
                expiresAt,
                purchase.PurchaseToken,
                ct);

            return PurchaseResult.Succeeded(productId, purchase.PurchaseToken);
        }
        catch (Plugin.InAppBilling.InAppBillingPurchaseException ex)
            when (ex.PurchaseError == Plugin.InAppBilling.PurchaseError.UserCancelled)
        {
            _logger?.LogInformation("User cancelled purchase of {ProductId}", productId);
            return PurchaseResult.Cancelled();
        }
        catch (Plugin.InAppBilling.InAppBillingPurchaseException ex)
        {
            _logger?.LogError(ex, "Purchase failed for {ProductId}: {Error}", productId, ex.PurchaseError);
            return PurchaseResult.Failed(ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during purchase of {ProductId}", productId);
            return PurchaseResult.Failed(ex.Message);
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }

    public async Task<bool> RestorePurchasesAsync(CancellationToken ct = default)
    {
        var billing = Plugin.InAppBilling.CrossInAppBilling.Current;
        try
        {
            var connected = await billing.ConnectAsync();
            if (!connected)
            {
                _logger?.LogWarning("Could not connect to billing service for restore");
                return false;
            }

            var purchases = await billing.GetPurchasesAsync(Plugin.InAppBilling.ItemType.Subscription);
            var activePurchase = purchases?.FirstOrDefault(p =>
                p.State == Plugin.InAppBilling.PurchaseState.Purchased);

            if (activePurchase != null)
            {
                if (activePurchase.IsAcknowledged == false)
                {
                    await billing.FinalizePurchaseAsync([activePurchase.PurchaseToken]);
                }

                var duration = BillingConstants.GetEstimatedDuration(activePurchase.ProductId);
                var expiresAt = DateTime.UtcNow.Add(duration);

                await _subscriptionService.UpdateTierAsync(
                    SubscriptionTier.Premium,
                    activePurchase.ProductId,
                    expiresAt,
                    activePurchase.PurchaseToken,
                    ct);

                _logger?.LogInformation("Restored subscription: {ProductId}", activePurchase.ProductId);
                return true;
            }

            // No active subscription found — downgrade to Free
            await _subscriptionService.UpdateTierAsync(SubscriptionTier.Free, ct: ct);
            _logger?.LogInformation("No active subscription found during restore");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restore purchases");
            return false;
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }

    public async Task<bool> HasActiveSubscriptionAsync(CancellationToken ct = default)
    {
        var billing = Plugin.InAppBilling.CrossInAppBilling.Current;
        try
        {
            var connected = await billing.ConnectAsync();
            if (!connected)
                return false;

            var purchases = await billing.GetPurchasesAsync(Plugin.InAppBilling.ItemType.Subscription);
            return purchases?.Any(p => p.State == Plugin.InAppBilling.PurchaseState.Purchased) ?? false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check active subscription");
            return false;
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }

#else
    public Task<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductInfo>>([]);

    public Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken ct = default)
        => Task.FromResult(PurchaseResult.Failed("Billing is not supported on this platform."));

    public Task<bool> RestorePurchasesAsync(CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<bool> HasActiveSubscriptionAsync(CancellationToken ct = default)
        => Task.FromResult(false);
#endif
}
