using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for UserDecoration entity.
/// </summary>
public class UserDecorationConfiguration : IEntityTypeConfiguration<UserDecoration>
{
    public void Configure(EntityTypeBuilder<UserDecoration> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.PurchasedAt)
            .IsRequired();

        builder.Property(d => d.ShopItemId)
            .IsRequired();

        // Indexes
        builder.HasIndex(d => d.ShopItemId);

        // Relationship to ShopItem (template)
        builder.HasOne(d => d.ShopItem)
            .WithMany()
            .HasForeignKey(d => d.ShopItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
