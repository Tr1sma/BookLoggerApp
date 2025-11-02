using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for ShopItem entity.
/// </summary>
public class ShopItemConfiguration : IEntityTypeConfiguration<ShopItem>
{
    public void Configure(EntityTypeBuilder<ShopItem> builder)
    {
        builder.HasKey(si => si.Id);

        builder.Property(si => si.ItemType)
            .IsRequired();

        builder.Property(si => si.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(si => si.Description)
            .HasMaxLength(1000);

        builder.Property(si => si.Cost)
            .IsRequired();

        builder.Property(si => si.ImagePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(si => si.UnlockLevel)
            .IsRequired();

        // Indexes
        builder.HasIndex(si => si.ItemType);
        builder.HasIndex(si => si.UnlockLevel);
        builder.HasIndex(si => si.IsAvailable);

        // Relationship to PlantSpecies (optional)
        builder.HasOne(si => si.PlantSpecies)
            .WithMany()
            .HasForeignKey(si => si.PlantSpeciesId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
