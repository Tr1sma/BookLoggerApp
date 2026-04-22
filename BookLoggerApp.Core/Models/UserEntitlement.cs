using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Entitlements;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Single-row table that stores the user's current subscription state
/// as reported by Google Play Billing (or a redeemed promo code).
/// The <see cref="AppSettings.CurrentTier"/> field is a denormalized
/// hot-read copy of <see cref="Tier"/>.
/// </summary>
public class UserEntitlement
{
    public Guid Id { get; set; }

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    public BillingPeriod? BillingPeriod { get; set; }

    /// <summary>SKU that granted this entitlement (e.g. <c>plus_yearly</c>). Null for Free.</summary>
    [MaxLength(100)]
    public string? ProductId { get; set; }

    [MaxLength(256)]
    public string? PurchaseToken { get; set; }

    [MaxLength(100)]
    public string? OrderId { get; set; }

    public DateTime? PurchasedAt { get; set; }

    /// <summary>Null for <see cref="BillingPeriod.Lifetime"/> or Free.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastVerifiedAt { get; set; }

    public bool AutoRenewing { get; set; }

    public bool InGracePeriod { get; set; }

    public bool IsInIntroductoryPrice { get; set; }

    public bool IsFamilyShared { get; set; }

    /// <summary>Reason string set when <see cref="Tier"/> was downgraded to Free.</summary>
    [MaxLength(64)]
    public string? LapseReason { get; set; }

    public DateTime? LapsedAt { get; set; }

    /// <summary>Hardcoded in-app code that granted this entitlement, if any.</summary>
    [MaxLength(64)]
    public string? PromoCodeRedeemed { get; set; }

    public DateTime? PromoExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
