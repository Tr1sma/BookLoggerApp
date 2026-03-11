using Microsoft.EntityFrameworkCore;

using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Singleton service that manages subscription tier with local SQLite persistence and in-memory caching.
/// Follows the same pattern as AppSettingsProvider.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private SubscriptionInfo? _cachedInfo;
    private DateTime _lastLoad = DateTime.MinValue;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    public event EventHandler? TierChanged;

    private static readonly HashSet<FeatureFlag> PremiumFeatures =
    [
        FeatureFlag.UnlimitedShelves,
        FeatureFlag.AdvancedStatistics,
        FeatureFlag.ExportFunctions,
        FeatureFlag.CustomThemes,
        FeatureFlag.AiRecommendations,
        FeatureFlag.AdFree
    ];

    public SubscriptionService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<SubscriptionTier> GetCurrentTierAsync(CancellationToken ct = default)
    {
        var info = await GetSubscriptionInfoAsync(ct);

        // Auto-downgrade if Premium has expired
        if (info.Tier == SubscriptionTier.Premium &&
            info.ExpiresAt.HasValue &&
            info.ExpiresAt.Value < DateTime.UtcNow)
        {
            await UpdateTierAsync(SubscriptionTier.Free, ct: ct);
            return SubscriptionTier.Free;
        }

        return info.Tier;
    }

    public async Task<bool> HasFeatureAccessAsync(FeatureFlag flag, CancellationToken ct = default)
    {
        var tier = await GetCurrentTierAsync(ct);

        if (tier == SubscriptionTier.Premium)
            return true;

        // Free users only have access to features NOT in PremiumFeatures
        return !PremiumFeatures.Contains(flag);
    }

    public Task RestorePurchasesAsync(CancellationToken ct = default)
    {
        // No-op. Use IBillingService.RestorePurchasesAsync() which calls UpdateTierAsync() internally.
        return Task.CompletedTask;
    }

    public async Task UpdateTierAsync(SubscriptionTier tier, string? productId = null,
        DateTime? expiresAt = null, string? purchaseToken = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var info = await context.SubscriptionInfos.FirstOrDefaultAsync(ct);
        if (info == null)
        {
            info = new SubscriptionInfo();
            context.SubscriptionInfos.Add(info);
        }

        SubscriptionTier oldTier = info.Tier;
        info.Tier = tier;
        info.ProductId = productId ?? info.ProductId;
        info.ExpiresAt = expiresAt ?? info.ExpiresAt;
        info.PurchaseToken = purchaseToken ?? info.PurchaseToken;
        info.PurchasedAt = tier == SubscriptionTier.Premium
            ? (info.PurchasedAt ?? DateTime.UtcNow)
            : null;
        info.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        // Update cache
        _cachedInfo = info;
        _lastLoad = DateTime.UtcNow;

        if (oldTier != tier)
        {
            TierChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void InvalidateCache()
    {
        _cachedInfo = null;
        _lastLoad = DateTime.MinValue;
    }

    private async Task<SubscriptionInfo> GetSubscriptionInfoAsync(CancellationToken ct = default)
    {
        if (_cachedInfo != null && DateTime.UtcNow - _lastLoad < CacheLifetime)
            return _cachedInfo;

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var info = await context.SubscriptionInfos.FirstOrDefaultAsync(ct);

        if (info == null)
        {
            info = new SubscriptionInfo
            {
                Tier = SubscriptionTier.Free
            };
            context.SubscriptionInfos.Add(info);
            await context.SaveChangesAsync(ct);
        }

        _cachedInfo = info;
        _lastLoad = DateTime.UtcNow;

        return info;
    }
}
