using BookLoggerApp.Core.Constants;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Configurable mock implementation of IBillingService for testing.
/// </summary>
public class MockBillingService : IBillingService
{
    public bool ShouldSucceedPurchase { get; set; } = true;
    public bool ShouldCancelPurchase { get; set; }
    public string? FailureMessage { get; set; }
    public bool HasSubscription { get; set; }

    private readonly List<ProductInfo> _products =
    [
        new ProductInfo
        {
            ProductId = BillingConstants.PremiumMonthly,
            Name = "Premium Monthly",
            LocalizedPrice = "€3.99",
            PriceAmount = 3.99m,
            CurrencyCode = "EUR",
            Description = "Monthly premium subscription"
        },
        new ProductInfo
        {
            ProductId = BillingConstants.PremiumYearly,
            Name = "Premium Yearly",
            LocalizedPrice = "€29.99",
            PriceAmount = 29.99m,
            CurrencyCode = "EUR",
            Description = "Yearly premium subscription"
        }
    ];

    public Task<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductInfo>>(_products);

    public Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken ct = default)
    {
        if (ShouldCancelPurchase)
            return Task.FromResult(PurchaseResult.Cancelled());

        if (!ShouldSucceedPurchase)
            return Task.FromResult(PurchaseResult.Failed(FailureMessage ?? "Purchase failed"));

        return Task.FromResult(PurchaseResult.Succeeded(productId, $"token_{productId}"));
    }

    public Task<bool> RestorePurchasesAsync(CancellationToken ct = default)
        => Task.FromResult(HasSubscription);

    public Task<bool> HasActiveSubscriptionAsync(CancellationToken ct = default)
        => Task.FromResult(HasSubscription);
}
