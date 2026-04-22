using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for the single-row UserEntitlement entity.
/// </summary>
public class UserEntitlementConfiguration : IEntityTypeConfiguration<UserEntitlement>
{
    public void Configure(EntityTypeBuilder<UserEntitlement> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Tier)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(SubscriptionTier.Free);

        builder.Property(e => e.BillingPeriod)
            .HasConversion<int?>();

        builder.Property(e => e.ProductId)
            .HasMaxLength(100);

        builder.Property(e => e.PurchaseToken)
            .HasMaxLength(256);

        builder.Property(e => e.OrderId)
            .HasMaxLength(100);

        builder.Property(e => e.LapseReason)
            .HasMaxLength(64);

        builder.Property(e => e.PromoCodeRedeemed)
            .HasMaxLength(64);

        builder.Property(e => e.AutoRenewing)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.InGracePeriod)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.IsInIntroductoryPrice)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.IsFamilyShared)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();
    }
}
