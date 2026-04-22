using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Thin helper around <see cref="IEntitlementService"/> that centralizes the
/// "throw <see cref="EntitlementRequiredException"/>" logic used by content
/// services (notes, shelves, goals, plants, decorations).
/// </summary>
public class FeatureGuard : IFeatureGuard
{
    private readonly IEntitlementService _entitlements;

    public FeatureGuard(IEntitlementService entitlements)
    {
        _entitlements = entitlements;
    }

    public bool HasAccess(FeatureKey feature) => _entitlements.HasAccess(feature);

    public void RequireAccess(FeatureKey feature, string? context = null)
    {
        if (_entitlements.HasAccess(feature))
        {
            return;
        }

        throw new EntitlementRequiredException(
            feature,
            FeaturePolicy.GetMinimumTier(feature),
            _entitlements.CurrentTier,
            context);
    }

    public void EnforceSoftLimit(FeatureKey feature, int currentCount, int limit, string? context = null)
    {
        if (_entitlements.HasAccess(feature))
        {
            return;
        }

        if (currentCount < limit)
        {
            return;
        }

        throw new EntitlementRequiredException(
            feature,
            FeaturePolicy.GetMinimumTier(feature),
            _entitlements.CurrentTier,
            context ?? $"Free tier limit reached ({limit}). Upgrade to continue.");
    }
}
