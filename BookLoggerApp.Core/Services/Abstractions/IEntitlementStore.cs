using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Persistence gateway for the single-row <see cref="UserEntitlement"/> table.
/// Kept behind an interface so <c>IEntitlementService</c> can be unit-tested
/// without a real database.
/// </summary>
public interface IEntitlementStore
{
    /// <summary>
    /// Returns the user's entitlement row, creating a default Free entry if none exists.
    /// </summary>
    Task<UserEntitlement> GetOrCreateAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists the given entitlement. The caller owns the instance and should update
    /// <see cref="UserEntitlement.UpdatedAt"/> before calling.
    /// </summary>
    Task SaveAsync(UserEntitlement entitlement, CancellationToken ct = default);
}
