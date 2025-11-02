using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for PlantSpecies entity.
/// </summary>
public class PlantSpeciesConfiguration : IEntityTypeConfiguration<PlantSpecies>
{
    public void Configure(EntityTypeBuilder<PlantSpecies> builder)
    {
        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ps => ps.Description)
            .HasMaxLength(1000);

        builder.Property(ps => ps.ImagePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(ps => ps.MaxLevel)
            .IsRequired();

        builder.Property(ps => ps.WaterIntervalDays)
            .IsRequired();

        builder.Property(ps => ps.GrowthRate)
            .IsRequired();

        builder.Property(ps => ps.BaseCost)
            .IsRequired();

        builder.Property(ps => ps.UnlockLevel)
            .IsRequired();

        // Indexes
        builder.HasIndex(ps => ps.Name);
        builder.HasIndex(ps => ps.UnlockLevel);

        // Relationship
        builder.HasMany(ps => ps.UserPlants)
            .WithOne(up => up.Species)
            .HasForeignKey(up => up.SpeciesId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
