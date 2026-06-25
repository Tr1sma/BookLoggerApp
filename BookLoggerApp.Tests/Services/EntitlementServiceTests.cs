using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// Entitlement lifecycle on load: expiry persists and runs the data-guard (BUG-09),
/// auto-renewing subs aren't downgraded on a guessed expiry (LOG-01), and a Plus grant
/// doesn't un-hide Premium content (SEC-04).
/// </summary>
public class EntitlementServiceTests : IDisposable
{
    private readonly InMemoryFactory _factory;
    private readonly IEntitlementStore _store;
    private readonly IAppSettingsProvider _settings;
    private readonly EntitlementLapseHandler _lapseHandler;

    public EntitlementServiceTests()
    {
        _factory = new InMemoryFactory(Guid.NewGuid().ToString());
        _store = Substitute.For<IEntitlementStore>();
        _settings = Substitute.For<IAppSettingsProvider>();
        _lapseHandler = new EntitlementLapseHandler(_factory);
    }

    public void Dispose()
    {
        using AppDbContext ctx = _factory.CreateDbContext();
        ctx.Database.EnsureDeleted();
    }

    private EntitlementService CreateService() => new(_store, _settings, _lapseHandler);

    [Fact]
    public async Task InitializeAsync_WhenExpired_PersistsFreeTierAndRunsDataGuard()
    {
        // BUG-09: a time-expired (non-auto-renewing) sub must persist as Free and run the
        // overflow-hide guard, not just flip the in-memory tier.
        var expired = new UserEntitlement
        {
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            AutoRenewing = false,
            ProductId = "plus_monthly",
            PurchaseToken = "tok",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(expired);

        Guid species = await SeedSpeciesAsync(isFree: true);
        await SeedPlantAsync(species, "First", new DateTime(2026, 1, 1), isActive: true);
        await SeedPlantAsync(species, "Second", new DateTime(2026, 2, 1), isActive: true);
        await SeedPlantAsync(species, "Third", new DateTime(2026, 3, 1), isActive: true);

        EntitlementService service = CreateService();
        await service.InitializeAsync();

        service.CurrentTier.Should().Be(SubscriptionTier.Free);
        await _store.Received().SaveAsync(
            Arg.Is<UserEntitlement>(e => e.Tier == SubscriptionTier.Free && e.LapseReason == "expired"),
            Arg.Any<CancellationToken>());

        await using AppDbContext ctx = _factory.CreateDbContext();
        (await ctx.UserPlants.CountAsync(p => p.IsActive)).Should().Be(1, "the lapse data-guard must reduce active plants to one");
    }

    [Fact]
    public async Task InitializeAsync_WhenAutoRenewing_DoesNotDowngradeEvenIfExpiresInPast()
    {
        // LOG-01: ExpiresAt is only an estimate for auto-renewing subs; Play presence is the
        // source of truth, so don't lapse an auto-renewing sub on a stale date.
        var autoRenew = new UserEntitlement
        {
            Tier = SubscriptionTier.Premium,
            BillingPeriod = BillingPeriod.Monthly,
            AutoRenewing = true,
            ProductId = "premium_monthly",
            PurchaseToken = "tok",
            ExpiresAt = DateTime.UtcNow.AddDays(-3)
        };
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(autoRenew);

        EntitlementService service = CreateService();
        await service.InitializeAsync();

        service.CurrentTier.Should().Be(SubscriptionTier.Premium);
        await _store.DidNotReceive().SaveAsync(
            Arg.Is<UserEntitlement>(e => e.Tier == SubscriptionTier.Free),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_OnFreeLoad_ReconcilesAndHidesNonEntitledContent()
    {
        // HIGH-1003: a Free initial load (e.g. after backup-restore wiped entitlements) must run
        // the data-guard to hide paid content in the restored DB — previously ran only on expiry.
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(new UserEntitlement { Tier = SubscriptionTier.Free });

        Guid standard = await SeedSpeciesAsync(isPrestige: false, isFree: false);
        await SeedPlantAsync(standard, "Standard", new DateTime(2026, 1, 1), isActive: true);

        EntitlementService service = CreateService();
        await service.InitializeAsync();

        service.CurrentTier.Should().Be(SubscriptionTier.Free);
        await using AppDbContext ctx = _factory.CreateDbContext();
        (await ctx.UserPlants.SingleAsync(p => p.Name == "Standard")).IsHiddenByEntitlement
            .Should().BeTrue("a Free initial load must hide non-free content reintroduced out-of-band");
    }

    [Fact]
    public async Task InitializeAsync_OnPremiumLoad_ReconcilesAndClearsEntitledHides()
    {
        // HIGH-1003: reconcile runs on every load but must not strip a paying user's content —
        // a Premium load un-hides prestige content it is entitled to.
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(new UserEntitlement { Tier = SubscriptionTier.Premium });

        Guid prestige = await SeedSpeciesAsync(isPrestige: true);
        await SeedPlantAsync(prestige, "Phoenix", new DateTime(2026, 1, 1), isActive: false);
        await using (AppDbContext setup = _factory.CreateDbContext())
        {
            UserPlant p = await setup.UserPlants.FirstAsync();
            p.IsHiddenByEntitlement = true;
            await setup.SaveChangesAsync();
        }

        EntitlementService service = CreateService();
        await service.InitializeAsync();

        service.CurrentTier.Should().Be(SubscriptionTier.Premium);
        await using AppDbContext ctx = _factory.CreateDbContext();
        (await ctx.UserPlants.SingleAsync(p => p.Name == "Phoenix")).IsHiddenByEntitlement
            .Should().BeFalse("Premium is entitled to prestige plants");
    }

    [Fact]
    public async Task RefreshAsync_WhenExpired_PersistsFreeTier()
    {
        var expired = new UserEntitlement
        {
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Yearly,
            AutoRenewing = false,
            ProductId = "plus_yearly",
            PurchaseToken = "tok",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(expired);

        EntitlementService service = CreateService();
        await service.RefreshAsync();

        service.CurrentTier.Should().Be(SubscriptionTier.Free);
        await _store.Received().SaveAsync(
            Arg.Is<UserEntitlement>(e => e.Tier == SubscriptionTier.Free && e.LapseReason == "expired"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_SameTier_DoesNotRaiseEntitlementChanged()
    {
        // Z.357: a no-op refresh (resume, periodic reconcile) must not fire EntitlementChanged.
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(new UserEntitlement { Tier = SubscriptionTier.Free });
        EntitlementService service = CreateService();
        await service.InitializeAsync();

        int raised = 0;
        service.EntitlementChanged += (_, _) => raised++;

        await service.RefreshAsync();

        raised.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAsync_TierChanged_RaisesWithRefreshReason()
    {
        // Z.357: a refresh that observes a real tier change still broadcasts, tagged Refresh.
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(new UserEntitlement { Tier = SubscriptionTier.Free });
        EntitlementService service = CreateService();
        await service.InitializeAsync();

        EntitlementChangedEventArgs? captured = null;
        service.EntitlementChanged += (_, e) => captured = e;

        _store.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(new UserEntitlement { Tier = SubscriptionTier.Plus, BillingPeriod = BillingPeriod.Lifetime });
        await service.RefreshAsync();

        captured.Should().NotBeNull();
        captured!.Reason.Should().Be(EntitlementChangeReason.Refresh);
        captured.PreviousTier.Should().Be(SubscriptionTier.Free);
        captured.CurrentTier.Should().Be(SubscriptionTier.Plus);
    }

    [Fact]
    public async Task ApplyPurchaseAsync_RestoreOfAlreadyStoredPurchase_DoesNotResaveOrRaise()
    {
        // Z.679: resume/restore re-applies active purchases each foreground. When the stored
        // entitlement already matches, ApplyPurchaseAsync short-circuits — no save, no event.
        var stored = new UserEntitlement
        {
            Tier = SubscriptionTier.Plus,
            BillingPeriod = BillingPeriod.Monthly,
            ProductId = "plus_monthly",
            PurchaseToken = "tok-123",
            AutoRenewing = true,
            ExpiresAt = DateTime.UtcNow.AddDays(20)
        };
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(stored);
        EntitlementService service = CreateService();
        await service.InitializeAsync();

        int raised = 0;
        service.EntitlementChanged += (_, _) => raised++;
        _store.ClearReceivedCalls();

        var purchase = new PurchaseResult(
            SubscriptionTier.Plus, BillingPeriod.Monthly, "plus_monthly", "tok-123", "order-1",
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(25), AutoRenewing: true,
            IsInIntroductoryPrice: false, IsFamilyShared: false);
        await service.ApplyPurchaseAsync(purchase, EntitlementChangeReason.Restore);

        raised.Should().Be(0);
        await _store.DidNotReceive().SaveAsync(Arg.Any<UserEntitlement>(), Arg.Any<CancellationToken>());
        service.CurrentTier.Should().Be(SubscriptionTier.Plus);
    }

    [Fact]
    public async Task ApplyPurchaseAsync_RestoreWithDifferentToken_AppliesAndPersists()
    {
        // Z.679 guard: a changed purchase token is a real change and must still apply on restore.
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(new UserEntitlement
            {
                Tier = SubscriptionTier.Plus,
                BillingPeriod = BillingPeriod.Monthly,
                ProductId = "plus_monthly",
                PurchaseToken = "tok-OLD",
                AutoRenewing = true
            });
        EntitlementService service = CreateService();
        await service.InitializeAsync();
        _store.ClearReceivedCalls();

        var purchase = new PurchaseResult(
            SubscriptionTier.Plus, BillingPeriod.Monthly, "plus_monthly", "tok-NEW", "order-2",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), AutoRenewing: true,
            IsInIntroductoryPrice: false, IsFamilyShared: false);
        await service.ApplyPurchaseAsync(purchase, EntitlementChangeReason.Restore);

        await _store.Received(1).SaveAsync(
            Arg.Is<UserEntitlement>(e => e.PurchaseToken == "tok-NEW"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyPurchaseAsync_Plus_DoesNotUnhidePrestigeContent()
    {
        // SEC-04 wiring: granting Plus routes the tier into the lapse handler so prestige
        // plants stay hidden.
        _store.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(new UserEntitlement { Tier = SubscriptionTier.Free });

        Guid prestige = await SeedSpeciesAsync(isPrestige: true);
        await SeedPlantAsync(prestige, "Phoenix", new DateTime(2026, 1, 1), isActive: true);
        await using (AppDbContext setup = _factory.CreateDbContext())
        {
            UserPlant p = await setup.UserPlants.FirstAsync();
            p.IsHiddenByEntitlement = true;
            await setup.SaveChangesAsync();
        }

        EntitlementService service = CreateService();
        var purchase = new PurchaseResult(
            SubscriptionTier.Plus, BillingPeriod.Monthly, "plus_monthly", "tok", null,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), AutoRenewing: true,
            IsInIntroductoryPrice: false, IsFamilyShared: false);
        await service.ApplyPurchaseAsync(purchase);

        await using AppDbContext ctx = _factory.CreateDbContext();
        (await ctx.UserPlants.SingleAsync(p => p.Name == "Phoenix")).IsHiddenByEntitlement
            .Should().BeTrue("Plus is not entitled to prestige plants");
    }

    private async Task<Guid> SeedSpeciesAsync(bool isPrestige = false, bool isFree = false)
    {
        await using AppDbContext ctx = _factory.CreateDbContext();
        PlantSpecies species = new()
        {
            Id = Guid.NewGuid(),
            Name = $"Species_{Guid.NewGuid():N}",
            ImagePath = "dummy.svg",
            IsPrestigeTier = isPrestige,
            IsFreeTier = isFree
        };
        ctx.PlantSpecies.Add(species);
        await ctx.SaveChangesAsync();
        return species.Id;
    }

    private async Task SeedPlantAsync(Guid speciesId, string name, DateTime plantedAt, bool isActive)
    {
        await using AppDbContext ctx = _factory.CreateDbContext();
        ctx.UserPlants.Add(new UserPlant
        {
            Id = Guid.NewGuid(),
            SpeciesId = speciesId,
            Name = name,
            PlantedAt = plantedAt,
            LastWatered = plantedAt,
            IsActive = isActive,
            Status = PlantStatus.Healthy
        });
        await ctx.SaveChangesAsync();
    }

    private sealed class InMemoryFactory : IDbContextFactory<AppDbContext>
    {
        private readonly string _dbName;
        public InMemoryFactory(string dbName) => _dbName = dbName;

        public AppDbContext CreateDbContext()
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;
            AppDbContext ctx = new(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }
    }
}
