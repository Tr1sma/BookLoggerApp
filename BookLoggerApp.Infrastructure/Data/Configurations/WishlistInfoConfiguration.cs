using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for WishlistInfo entity.
/// </summary>
public class WishlistInfoConfiguration : IEntityTypeConfiguration<WishlistInfo>
{
    public void Configure(EntityTypeBuilder<WishlistInfo> builder)
    {
        builder.HasKey(w => w.BookId);

        builder.HasOne(w => w.Book)
            .WithOne(b => b.WishlistInfo)
            .HasForeignKey<WishlistInfo>(w => w.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(w => w.RecommendedBy)
            .HasMaxLength(200);

        builder.Property(w => w.WishlistNotes)
            .HasMaxLength(1000);

        builder.HasIndex(w => w.Priority);
        builder.HasIndex(w => w.DateAddedToWishlist);
    }
}
