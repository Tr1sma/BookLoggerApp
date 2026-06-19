using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// CODE_REVIEW BUG-09: EntitlementService.EvaluateExpiryIfNeeded mutated the
/// entitlement object in-memory only — it never called SaveAsync and never
/// triggered EntitlementLapseHandler. Expired subscriptions were silently
/// displayed as Free in the current session but the DB row kept Tier=Plus,
/// so the next cold-start re-evaluated the same expired row and the lapse
/// handler was never invoked (overflow plants stayed visible, etc.).
///
/// The fix routes the expiry check through the full ApplyLapseAsync path.
/// </summary>
public class EntitlementServiceTests
{
    private static UserEntitlement MakeExpiredEntitlement() => new()
    {
        Id = Guid.NewGuid(),
        Tier = SubscriptionTier.Plus,
        BillingPeriod = BillingPeriod.Monthly,
        ExpiresAt = DateTime.UtcNow.AddDays(-1),
        AutoRenewing = false,
        CreatedAt = DateTime.UtcNow.AddDays(-40)
    };

    private static EntitlementService BuildService(IEntitlementStore store, IAppSettingsProvider? settings = null)
    {
        settings ??= Substitute.For<IAppSettingsProvider>();
        settings.UpdateEntitlementMirrorAsync(
            Arg.Any<SubscriptionTier>(),
            Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return new EntitlementService(store, settings);
    }

    [Fact]
    public async Task InitializeAsync_routes_expired_subscription_through_full_lapse_path_and_persists()
    {
        UserEntitlement expired = MakeExpiredEntitlement();
        IEntitlementStore store = Substitute.For<IEntitlementStore>();
        store.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(expired);

        EntitlementService service = BuildService(store);

        await service.InitializeAsync();

        service.CurrentTier.Should().Be(SubscriptionTier.Free);
        service.CurrentEntitlement!.LapseReason.Should().Be("expired");
        // SaveAsync must be called — that's what the old code never did.
        await store.Received().SaveAsync(
            Arg.Is<UserEntitlement>(u => u.Tier == SubscriptionTier.Free && u.LapseReason == "expired"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_routes_expired_subscription_through_full_lapse_path_and_persists()
    {
        UserEntitlement expired = MakeExpiredEntitlement();
        IEntitlementStore store = Substitute.For<IEntitlementStore>();
        store.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(expired);

        EntitlementService service = BuildService(store);

        await service.RefreshAsync();

        service.CurrentTier.Should().Be(SubscriptionTier.Free);
        await store.Received().SaveAsync(
            Arg.Is<UserEntitlement>(u => u.Tier == SubscriptionTier.Free),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_does_not_lapse_auto_renewing_subscription_even_when_past_nominal_expiry()
    {
        // AutoRenewing=true means the billing provider will renew imminently.
        // The estimated ExpiresAt (transaction + 30 days) is unreliable for these subs;
        // never lapse while the server says the sub is still active.
        UserEntitlement autoRenewing = new()
        {
            Id = Guid.NewGuid(),
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            AutoRenewing = true
        };
        IEntitlementStore store = Substitute.For<IEntitlementStore>();
        store.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(autoRenewing);

        EntitlementService service = BuildService(store);

        await service.InitializeAsync();

        service.CurrentTier.Should().Be(SubscriptionTier.Plus, "auto-renewing subs must not be silently lapsed");
        await store.DidNotReceive().SaveAsync(
            Arg.Is<UserEntitlement>(u => u.Tier == SubscriptionTier.Free),
            Arg.Any<CancellationToken>());
    }
}
