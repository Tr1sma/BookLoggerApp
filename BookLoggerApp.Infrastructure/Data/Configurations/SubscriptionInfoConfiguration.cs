using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

public class SubscriptionInfoConfiguration : IEntityTypeConfiguration<SubscriptionInfo>
{
    public void Configure(EntityTypeBuilder<SubscriptionInfo> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Tier)
            .IsRequired();

        builder.Property(s => s.ProductId)
            .HasMaxLength(100);

        builder.Property(s => s.PurchaseToken)
            .HasMaxLength(500);

        builder.Property(s => s.CreatedAt)
            .IsRequired();
    }
}
