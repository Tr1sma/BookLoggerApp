using System.ComponentModel.DataAnnotations;

using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Stores the user's subscription status locally (single-row table).
/// Persisted in SQLite to survive app restarts.
/// </summary>
public class SubscriptionInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Current subscription tier (Free or Premium).
    /// </summary>
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    /// <summary>
    /// When the subscription was originally purchased (null for Free).
    /// </summary>
    public DateTime? PurchasedAt { get; set; }

    /// <summary>
    /// When the current subscription period expires (null for Free or lifetime).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Google Play product ID (e.g., "premium_monthly", "premium_yearly").
    /// Populated by billing integration (Issue #2).
    /// </summary>
    [MaxLength(100)]
    public string? ProductId { get; set; }

    /// <summary>
    /// Google Play purchase token for verification.
    /// Populated by billing integration (Issue #2).
    /// </summary>
    [MaxLength(500)]
    public string? PurchaseToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
