using System;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class FeatureGuardTests
{
    [Fact]
    public void RequireAccess_Free_throws_for_Plus_feature()
    {
        FeatureGuard guard = BuildGuard(SubscriptionTier.Free);

        Action act = () => guard.RequireAccess(FeatureKey.UnlimitedNotesAndQuotes);

        act.Should().Throw<EntitlementRequiredException>()
            .Which.RequiredTier.Should().Be(SubscriptionTier.Plus);
    }

    [Fact]
    public void RequireAccess_Plus_passes_for_Plus_feature()
    {
        FeatureGuard guard = BuildGuard(SubscriptionTier.Plus);

        Action act = () => guard.RequireAccess(FeatureKey.UnlimitedNotesAndQuotes);

        act.Should().NotThrow();
    }

    [Fact]
    public void RequireAccess_Plus_throws_for_Premium_feature()
    {
        FeatureGuard guard = BuildGuard(SubscriptionTier.Plus);

        Action act = () => guard.RequireAccess(FeatureKey.StatsTrendsTab);

        act.Should().Throw<EntitlementRequiredException>()
            .Which.RequiredTier.Should().Be(SubscriptionTier.Premium);
    }

    [Fact]
    public void RequireAccess_Premium_passes_for_Premium_feature()
    {
        FeatureGuard guard = BuildGuard(SubscriptionTier.Premium);

        Action act = () => guard.RequireAccess(FeatureKey.StatsTrendsTab);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnforceSoftLimit_below_limit_passes_on_Free()
    {
        FeatureGuard guard = BuildGuard(SubscriptionTier.Free);

        Action act = () => guard.EnforceSoftLimit(FeatureKey.UnlimitedNotesAndQuotes, currentCount: 2, limit: 3);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnforceSoftLimit_at_limit_throws_on_Free()
    {
        FeatureGuard guard = BuildGuard(SubscriptionTier.Free);

        Action act = () => guard.EnforceSoftLimit(FeatureKey.UnlimitedNotesAndQuotes, currentCount: 3, limit: 3);

        act.Should().Throw<EntitlementRequiredException>()
            .Which.Feature.Should().Be(FeatureKey.UnlimitedNotesAndQuotes);
    }

    [Fact]
    public void EnforceSoftLimit_ignores_limit_on_Plus()
    {
        FeatureGuard guard = BuildGuard(SubscriptionTier.Plus);

        Action act = () => guard.EnforceSoftLimit(FeatureKey.UnlimitedNotesAndQuotes, currentCount: 999, limit: 3);

        act.Should().NotThrow();
    }

    [Fact]
    public void HasAccess_matches_EntitlementService()
    {
        FeatureGuard guard = BuildGuard(SubscriptionTier.Premium);

        guard.HasAccess(FeatureKey.StatsTrendsTab).Should().BeTrue();
        guard.HasAccess(FeatureKey.UnlimitedNotesAndQuotes).Should().BeTrue();
    }

    private static FeatureGuard BuildGuard(SubscriptionTier tier)
    {
        IEntitlementService entitlements = new FakeEntitlementService(tier);
        return new FeatureGuard(entitlements);
    }
}
