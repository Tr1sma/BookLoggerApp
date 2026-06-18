using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Billing stub for non-Android heads and pre-Play-Billing development.
/// Every operation is a safe no-op so paywall UI can render.
/// </summary>
public class NoOpBillingService : IBillingService
{
    public static NoOpBillingService Instance { get; } = new();

    public bool IsConnected => false;

    public event EventHandler<PurchaseResult>? PurchaseUpdated
    {
        add { }
        remove { }
    }

    public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<BillingProduct>> QueryProductsAsync(IEnumerable<string> productIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BillingProduct>>(Array.Empty<BillingProduct>());

    public Task<IReadOnlyList<PurchaseResult>> QueryActivePurchasesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PurchaseResult>>(Array.Empty<PurchaseResult>());

    public Task<BillingPurchaseOutcome> LaunchPurchaseFlowAsync(string productId, string? oldPurchaseToken = null, CancellationToken ct = default) =>
        Task.FromResult(BillingPurchaseOutcome.BillingUnavailable);

    public Task AcknowledgePurchaseAsync(string purchaseToken, CancellationToken ct = default) => Task.CompletedTask;

    public Task<bool> LaunchRedeemPromoFlowAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<bool> OpenSubscriptionManagementAsync(string? productId = null, CancellationToken ct = default) => Task.FromResult(false);
}
