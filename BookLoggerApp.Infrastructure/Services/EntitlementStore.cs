using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// EF Core-backed implementation of <see cref="IEntitlementStore"/>. Creates
/// a fresh DbContext per call (via <see cref="IDbContextFactory{TContext}"/>)
/// so it is safe to register as a Singleton.
/// </summary>
public class EntitlementStore : IEntitlementStore
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public EntitlementStore(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<UserEntitlement> GetOrCreateAsync(CancellationToken ct = default)
    {
        await using AppDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        UserEntitlement? entitlement = await context.UserEntitlements.FirstOrDefaultAsync(ct);
        if (entitlement is not null)
        {
            return entitlement;
        }

        entitlement = new UserEntitlement
        {
            Id = Guid.NewGuid(),
            Tier = SubscriptionTier.Free,
            CreatedAt = DateTime.UtcNow
        };
        context.UserEntitlements.Add(entitlement);
        await context.SaveChangesAsync(ct);
        return entitlement;
    }

    public async Task SaveAsync(UserEntitlement entitlement, CancellationToken ct = default)
    {
        await using AppDbContext context = await _contextFactory.CreateDbContextAsync(ct);

        entitlement.UpdatedAt = DateTime.UtcNow;
        context.UserEntitlements.Update(entitlement);
        await context.SaveChangesAsync(ct);
    }
}
