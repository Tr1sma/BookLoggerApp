using BookLoggerApp.Core.Entitlements;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Helper used by services to enforce entitlement rules and throw
/// <see cref="Exceptions.EntitlementRequiredException"/> on violation. Keeps the
/// "check, throw, propagate" plumbing out of every service method.
/// </summary>
public interface IFeatureGuard
{
    /// <summary>Throws if the user does not currently hold access to <paramref name="feature"/>.</summary>
    void RequireAccess(FeatureKey feature, string? context = null);

    /// <summary>
    /// Throws if the user is below the feature's minimum tier AND <paramref name="currentCount"/>
    /// has already reached <paramref name="limit"/>. Used for Free-tier soft caps like
    /// "max 3 notes per book".
    /// </summary>
    void EnforceSoftLimit(FeatureKey feature, int currentCount, int limit, string? context = null);

    /// <summary>Shortcut: <c>IEntitlementService.HasAccess(feature)</c>.</summary>
    bool HasAccess(FeatureKey feature);
}
