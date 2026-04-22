namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Billing cadence for a purchased subscription or one-time product.
/// Lifetime represents a non-consumable managed product; subscription fields do not apply.
/// </summary>
public enum BillingPeriod
{
    Monthly = 0,
    Yearly = 1,
    Lifetime = 2
}
