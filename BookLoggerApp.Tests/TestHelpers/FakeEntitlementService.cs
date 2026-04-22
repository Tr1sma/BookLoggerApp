using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// In-memory fake of <see cref="IEntitlementService"/> for unit tests that don't
/// need to exercise Play Billing. Call <see cref="SetTier"/> to flip the state.
/// </summary>
public class FakeEntitlementService : IEntitlementService
{
    private UserEntitlement _current;

    public FakeEntitlementService(SubscriptionTier tier = SubscriptionTier.Free)
    {
        _current = new UserEntitlement
        {
            Id = Guid.NewGuid(),
            Tier = tier,
            CreatedAt = DateTime.UtcNow
        };
        IsInitialized = true;
    }

    public event EventHandler<EntitlementChangedEventArgs>? EntitlementChanged;

    public SubscriptionTier CurrentTier => _current.Tier;
    public UserEntitlement? CurrentEntitlement => _current;
    public bool IsInitialized { get; private set; }

    public void SetTier(SubscriptionTier tier, EntitlementChangeReason reason = EntitlementChangeReason.DebugForce)
    {
        SubscriptionTier previous = _current.Tier;
        _current = new UserEntitlement
        {
            Id = _current.Id,
            Tier = tier,
            CreatedAt = _current.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        EntitlementChanged?.Invoke(this, new EntitlementChangedEventArgs(previous, tier, _current, reason));
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        IsInitialized = true;
        return Task.CompletedTask;
    }

    public bool HasAccess(FeatureKey feature) => FeaturePolicy.IsUnlockedFor(feature, _current.Tier);

    public Task<bool> HasAccessAsync(FeatureKey feature, CancellationToken ct = default) => Task.FromResult(HasAccess(feature));

    public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ApplyPurchaseAsync(PurchaseResult purchase, EntitlementChangeReason reason = EntitlementChangeReason.Purchase, CancellationToken ct = default)
    {
        SetTier(purchase.Tier, reason);
        return Task.CompletedTask;
    }

    public Task ApplyLapseAsync(string reason, CancellationToken ct = default)
    {
        SetTier(SubscriptionTier.Free, EntitlementChangeReason.Lapse);
        return Task.CompletedTask;
    }

    public Task ApplyPromoAsync(PromoActivation promo, CancellationToken ct = default)
    {
        SetTier(promo.GrantedTier, EntitlementChangeReason.Promo);
        return Task.CompletedTask;
    }

    public Task ForceTierForDebugAsync(SubscriptionTier tier, CancellationToken ct = default)
    {
        SetTier(tier, EntitlementChangeReason.DebugForce);
        return Task.CompletedTask;
    }
}
