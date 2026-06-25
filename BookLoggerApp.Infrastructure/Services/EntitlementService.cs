using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Services.Analytics;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Singleton entitlement cache. Persists via <see cref="IEntitlementStore"/>, mirrors the tier
/// into <c>AppSettings.CurrentTier</c> for hot-path reads, and broadcasts <see cref="EntitlementChanged"/>.
/// </summary>
public class EntitlementService : IEntitlementService
{
    private readonly IEntitlementStore _store;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly EntitlementLapseHandler? _lapseHandler;
    private readonly IAnalyticsService _analytics;

    private readonly SemaphoreSlim _initGate = new(1, 1);
    private UserEntitlement? _current;
    private bool _isInitialized;

    public event EventHandler<EntitlementChangedEventArgs>? EntitlementChanged;

    public EntitlementService(
        IEntitlementStore store,
        IAppSettingsProvider settingsProvider,
        EntitlementLapseHandler? lapseHandler = null,
        IAnalyticsService? analytics = null)
    {
        _store = store;
        _settingsProvider = settingsProvider;
        _lapseHandler = lapseHandler;
        _analytics = analytics ?? NoOpAnalyticsService.Instance;
    }

    public SubscriptionTier CurrentTier => _current?.Tier ?? SubscriptionTier.Free;

    public UserEntitlement? CurrentEntitlement => _current;

    public bool IsInitialized => _isInitialized;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initGate.WaitAsync(ct);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            UserEntitlement entitlement = await _store.GetOrCreateAsync(ct);

            if (ShouldLapseByExpiry(entitlement))
            {
                // Persist the downgrade and run the data-guard, not a silent in-memory flip (BUG-09).
                await ApplyLapseAsync("expired", ct);
                return;
            }

            SubscriptionTier previous = SubscriptionTier.Free;
            _current = entitlement;
            _isInitialized = true;

            // HIGH-1003: reconcile content against the real tier on every load (restore/import can
            // reintroduce higher-tier content for a lower-tier user). Idempotent.
            if (_lapseHandler is not null)
            {
                await _lapseHandler.ReconcileAsync(entitlement.Tier, ct);
            }

            await SyncAppSettingsMirrorAsync(entitlement, ct);
            Raise(previous, entitlement, EntitlementChangeReason.InitialLoad);
        }
        finally
        {
            _initGate.Release();
        }
    }

    public bool HasAccess(FeatureKey feature)
    {
        if (!_isInitialized)
        {
            return false;
        }
        return FeaturePolicy.IsUnlockedFor(feature, CurrentTier);
    }

    public async Task<bool> HasAccessAsync(FeatureKey feature, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }
        return FeaturePolicy.IsUnlockedFor(feature, CurrentTier);
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        UserEntitlement reloaded = await _store.GetOrCreateAsync(ct);

        if (ShouldLapseByExpiry(reloaded))
        {
            await ApplyLapseAsync("expired", ct);
            return;
        }

        SubscriptionTier previous = _current?.Tier ?? SubscriptionTier.Free;
        _current = reloaded;
        _isInitialized = true;

        // HIGH-1003: same reconcile pass as InitializeAsync.
        if (_lapseHandler is not null)
        {
            await _lapseHandler.ReconcileAsync(reloaded.Tier, ct);
        }

        await SyncAppSettingsMirrorAsync(reloaded, ct);

        // Broadcast only on an actual tier change; a same-tier refresh would churn every subscriber.
        if (previous != reloaded.Tier)
        {
            Raise(previous, reloaded, EntitlementChangeReason.Refresh);
        }
    }

    public async Task ApplyPurchaseAsync(PurchaseResult purchase, EntitlementChangeReason reason = EntitlementChangeReason.Purchase, CancellationToken ct = default)
    {
        UserEntitlement current = await _store.GetOrCreateAsync(ct);

        // Restore/resume re-applies every active Play purchase per foreground; short-circuit when the
        // stored entitlement already matches (no DB write, no broadcast). Genuine purchases fall through.
        if (reason == EntitlementChangeReason.Restore && MatchesStoredPurchase(current, purchase))
        {
            _current = current;
            _isInitialized = true;
            return;
        }

        SubscriptionTier previous = current.Tier;

        current.Tier = purchase.Tier;
        current.BillingPeriod = purchase.Period;
        current.ProductId = purchase.ProductId;
        current.PurchaseToken = purchase.PurchaseToken;
        current.OrderId = purchase.OrderId;
        current.PurchasedAt = purchase.PurchasedAt;
        current.ExpiresAt = purchase.ExpiresAt;
        current.AutoRenewing = purchase.AutoRenewing;
        current.IsInIntroductoryPrice = purchase.IsInIntroductoryPrice;
        current.IsFamilyShared = purchase.IsFamilyShared;
        current.InGracePeriod = false;
        current.LastVerifiedAt = DateTime.UtcNow;
        current.LapseReason = null;
        current.LapsedAt = null;
        current.PromoCodeRedeemed = null;
        current.PromoExpiresAt = null;

        await _store.SaveAsync(current, ct);
        _current = current;
        _isInitialized = true;

        if (purchase.Tier >= SubscriptionTier.Plus && _lapseHandler is not null)
        {
            await _lapseHandler.ClearEntitlementHidesAsync(purchase.Tier, ct);
        }

        await SyncAppSettingsMirrorAsync(current, ct);
        Raise(previous, current, reason);
    }

    public async Task ApplyLapseAsync(string reason, CancellationToken ct = default)
    {
        UserEntitlement current = await _store.GetOrCreateAsync(ct);
        SubscriptionTier previous = current.Tier;

        current.Tier = SubscriptionTier.Free;
        current.BillingPeriod = null;
        current.ExpiresAt = null;
        current.AutoRenewing = false;
        current.InGracePeriod = false;
        current.IsInIntroductoryPrice = false;
        current.LapseReason = reason;
        current.LapsedAt = DateTime.UtcNow;

        await _store.SaveAsync(current, ct);
        _current = current;
        _isInitialized = true;

        if (_lapseHandler is not null)
        {
            await _lapseHandler.ApplyLapseAsync(ct);
        }

        _analytics.LogEvent(AnalyticsEventNames.SubscriptionLapsed, AnalyticsParamBuilder.Create()
            .Add(AnalyticsParamNames.Tier, previous.ToString())
            .Add(AnalyticsParamNames.Reason, reason)
            .BuildMutable());

        await SyncAppSettingsMirrorAsync(current, ct);
        Raise(previous, current, EntitlementChangeReason.Lapse);
    }

    public async Task ApplyPromoAsync(PromoActivation promo, CancellationToken ct = default)
    {
        UserEntitlement current = await _store.GetOrCreateAsync(ct);
        SubscriptionTier previous = current.Tier;

        current.Tier = promo.GrantedTier;
        current.BillingPeriod = promo.GrantedPeriod;
        current.ProductId = null;
        current.PurchaseToken = null;
        current.OrderId = null;
        current.PurchasedAt = DateTime.UtcNow;
        current.ExpiresAt = promo.ExpiresAt;
        current.AutoRenewing = false;
        current.IsInIntroductoryPrice = false;
        current.IsFamilyShared = false;
        current.InGracePeriod = false;
        current.LastVerifiedAt = DateTime.UtcNow;
        current.PromoCodeRedeemed = promo.Code;
        current.PromoExpiresAt = promo.ExpiresAt;
        current.LapseReason = null;
        current.LapsedAt = null;

        await _store.SaveAsync(current, ct);
        _current = current;
        _isInitialized = true;

        if (promo.GrantedTier >= SubscriptionTier.Plus && _lapseHandler is not null)
        {
            await _lapseHandler.ClearEntitlementHidesAsync(promo.GrantedTier, ct);
        }

        await SyncAppSettingsMirrorAsync(current, ct);
        Raise(previous, current, EntitlementChangeReason.Promo);
    }

    public async Task ForceTierForDebugAsync(SubscriptionTier tier, CancellationToken ct = default)
    {
        UserEntitlement current = await _store.GetOrCreateAsync(ct);
        SubscriptionTier previous = current.Tier;

        current.Tier = tier;
        current.BillingPeriod = tier == SubscriptionTier.Free ? null : BillingPeriod.Lifetime;
        current.ExpiresAt = null;
        current.AutoRenewing = false;
        current.InGracePeriod = false;
        current.IsInIntroductoryPrice = false;
        current.IsFamilyShared = false;
        current.LastVerifiedAt = DateTime.UtcNow;
        current.PromoCodeRedeemed = tier == SubscriptionTier.Free ? null : "BH-DEBUG";
        current.PromoExpiresAt = null;
        current.LapseReason = tier == SubscriptionTier.Free ? "debug_force" : null;
        current.LapsedAt = tier == SubscriptionTier.Free ? DateTime.UtcNow : null;

        await _store.SaveAsync(current, ct);
        _current = current;
        _isInitialized = true;

        if (_lapseHandler is not null)
        {
            if (tier == SubscriptionTier.Free)
            {
                await _lapseHandler.ApplyLapseAsync(ct);
            }
            else
            {
                await _lapseHandler.ClearEntitlementHidesAsync(tier, ct);
            }
        }

        await SyncAppSettingsMirrorAsync(current, ct);
        Raise(previous, current, EntitlementChangeReason.DebugForce);
    }

    /// <summary>True when the stored entitlement already matches this purchase's identity (tier + product + token).</summary>
    private static bool MatchesStoredPurchase(UserEntitlement stored, PurchaseResult purchase)
        => stored.Tier == purchase.Tier
           && string.Equals(stored.ProductId, purchase.ProductId, StringComparison.Ordinal)
           && string.Equals(stored.PurchaseToken, purchase.PurchaseToken, StringComparison.Ordinal);

    /// <summary>
    /// True when a loaded entitlement's term has passed (read-only check). Auto-renewing subscriptions
    /// are never lapsed on a guessed ExpiresAt — Google Play presence is the source of truth (LOG-01).
    /// </summary>
    private static bool ShouldLapseByExpiry(UserEntitlement entitlement)
    {
        if (entitlement.Tier == SubscriptionTier.Free)
        {
            return false;
        }

        if (entitlement.BillingPeriod == BillingPeriod.Lifetime)
        {
            return false;
        }

        if (entitlement.AutoRenewing)
        {
            return false;
        }

        return entitlement.ExpiresAt is { } expires && expires <= DateTime.UtcNow;
    }

    private async Task SyncAppSettingsMirrorAsync(UserEntitlement entitlement, CancellationToken ct)
    {
        try
        {
            // Narrow update: touches only CurrentTier/EntitlementExpiresAt so it can't clobber a
            // concurrent XP/coin/level award (SEC-12); the provider serialises AppSettings writes.
            await _settingsProvider.UpdateEntitlementMirrorAsync(entitlement.Tier, entitlement.ExpiresAt, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EntitlementService.SyncAppSettingsMirror failed: {ex}");
        }
    }

    private void Raise(SubscriptionTier previous, UserEntitlement snapshot, EntitlementChangeReason reason)
    {
        EventHandler<EntitlementChangedEventArgs>? handlers = EntitlementChanged;
        if (handlers is null)
        {
            return;
        }

        EntitlementChangedEventArgs args = new(previous, snapshot.Tier, snapshot, reason);
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler<EntitlementChangedEventArgs>)handler).Invoke(this, args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EntitlementChanged handler threw: {ex}");
            }
        }
    }
}
